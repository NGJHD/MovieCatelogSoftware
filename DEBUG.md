# Debugging the scraper

Everything here was learned by breaking `MovieSelector/IMDB Scrapper.cs` and fixing it again. It
is written down because every one of these mistakes looked correct in review and only showed up
against the live services — and because two of them silently wrote **wrong data** that looked
complete, which is worse than a clean failure.

## The one rule

> **A wrong ID is worse than no ID.**

A movie that fails to scrape is reported as `FAILED`, stays visible, and gets picked up by a
rescrape. A movie that resolves to the *wrong* IMDb entry looks finished forever: `Rescrap Movies
No ID` skips anything that already has an ID, so nothing ever revisits it. Every loosening of a
match rule has to be judged against that. When in doubt, fail.

## How a name becomes an ID

```
folder / file name
   │
   ├─ splitNameIntoTitleAndYear   cut at the year; that also drops the release junk
   ├─ buildTitleVariants          up to 3 ways the name might be written
   │
   ├─ OMDb  t=title&y=year        ─┐
   ├─ OMDb  t=title                ├─ EXACT normalised title only
   ├─ OMDb  s=title                ─┘
   │
   ├─ Yahoo scrape                ─┐  all IDs on the page, in order, each
   └─ DuckDuckGo lite scrape      ─┘  checked against OMDb; looser matching
```

Loose matching is allowed **only** on search-engine candidates, because a search engine has
already ranked them by relevance. OMDb's own index gets no such benefit — see below for why.

## Traps, each of which shipped a bug

### 1. Never take the first `imdb.com/title/tt…` on a search page

Yahoo puts a **knowledge panel** at the top of a film search — poster, rating, "Trailers &
Clips" (`m:KgMoviesYKC`, `t3:srpEntityHeader` in the markup). Its IMDb link points at a
*different entity* than the film's own page:

| Search | 1st ID on page | Correct ID (2nd) |
|---|---|---|
| `imdb Spider Man Homecoming 2017` | `tt2990104` — OMDb has no record | `tt2250912` |
| `imdb The Fall Guy 2024` | `tt13153468` — OMDb has no record | `tt1684562` |
| `imdb Maleficent 2014` | (panel agreed that day) | `tt1587310` |

The panel is volatile. It once returned `tt6824488` for Maleficent — *"Untitled Disney
Live-Action Project" (2020)*, the third film's placeholder. That one **resolves in OMDb**, so it
was written to the database as a complete entry.

Collect every ID on the page, in order, and let OMDb arbitrate.

### 2. OMDb's `t=` exact matcher returns companion pieces

This is the big one — it produced 17 wrong IDs across a 454-movie library.

IMDb carries a great deal of material *about* films, typed as `movie`, dated to the same year,
named after the film:

```
t=Good Will Hunting&y=1997               -> The Making of 'Good Will Hunting'
t=Harry Potter and the Half Blood Prince -> Big Movie Premiere: Harry Potter and the...
t=Fantastic Mr Fox&y=2009                -> Fantastic Mr Fox: T4 Movie Special
t=Frozen 2&y=2019                        -> Frozen 2 - Priyanka Chopra... - Promo
t=The Mitchells vs The Machines&y=2021   -> Subculture Film Reviews - THE MITCHELLS...
```

Worse, some of these entries **have the year inside their own title**, which defeats any
normalise-then-prefix comparison:

```
t=Memento&y=2000  ->  "Memento (2000)"   normalised: "memento2000"
t=1917&y=2019     ->  "1917 (2019)"      "19172019".StartsWith("1917") == true
```

Meanwhile `s=` usually lists the **correct** film first. The fix was not a better fuzzy match —
it was to stop being fuzzy at all here: **OMDb index results must match the title exactly** once
punctuation is normalised away (`Godzilla vs Kong` ≡ `Godzilla vs. Kong`). Anything else falls
through to the search engines, which rank these properly.

### 3. `duckduckgo.com/?q=` renders nothing

The main page is a JavaScript shell. Scraping it returned ~7.6 KB with **zero** IMDb links for
every query tried, so the fallback silently contributed nothing and any movie Yahoo missed was
simply lost. Use `https://lite.duckduckgo.com/lite/?q=` — plain server-rendered HTML.

### 4. DuckDuckGo's throttle page is HTTP **202**

Not 429, not 503 — **202 Accepted**, ~14 KB of "anomaly challenge" HTML. `IsSuccessStatusCode`
returns *true* for 202, so the challenge page was being parsed as search results. `getUrlData`
treats 202 as throttling and retries.

### 5. Search engines throttle by source address, so concurrency is the enemy

The scraper runs up to 5 threads. Yahoo and DuckDuckGo rate-limit per IP, and once tripped they
stay tripped, losing whole runs of movies rather than one. In a *single-threaded* test of 32
titles, 5 failed purely from throttling and all 5 resolved when re-run spaced out.

Search-engine fetches are now serialised behind `searchEngineLock`, held across the retries so a
throttled request does not have four more queueing into the same window. **OMDb is deliberately
not locked** — it is an API that answers concurrent requests happily, and there are far more of
those calls to get through.

The first retry is 5s. A second was not long enough for a throttle window to lift.

### 6. Beware the Latin residue of a non-Latin title

`buildTitleVariants` strips non-Latin script so OMDb can be searched. Applied to
`绣春刀II修罗战场` that leaves **`"II"`** — and OMDb has a Spanish film actually called *II*
(2017), the same year, an exact match. It was accepted.

A remnant of fewer than 4 alphanumeric characters, from a source that contained non-Latin
characters, is noise and is discarded.

### 7. IMDb carries films twice: an empty stub and the real entry

```
t=Jim Button and Luke the Engine Driver&y=2018  ->  tt8876778   no plot, no director, no country
t=Jim Button and Luke the Engine Driver         ->  tt3072732   2019, Germany, Dennis Gansel
```

Asking *with* the year pins the stub. A strict match that turns out to be a placeholder (no plot
**and** no director **and** no country) is now held aside rather than accepted, and applied only
if nothing better is found anywhere.

### 8. The looser the title match, the more the record must prove itself

`Dragon Tomb (2020)` shares both significant words with `The Dragon Tomb - Ancient Legend (2021)`
and the year was one out, so token overlap accepted it. It was wrong.

The discriminator that held across every case: **the correct entry has a plot; the wrong one
does not.** So an *approximate* title match must also belong to a record OMDb holds a plot for.
Exact and prefix matches are trusted without it.

### 9. `N/A` is OMDb's empty, not `null`

`Plot`, `Director`, `Country` and `imdbRating` all come back as the literal string `"N/A"`.
`String.IsNullOrEmpty` does not catch it. Use `hasValue()`.

## Sanity checks when something looks wrong

Reproduce against the live services before changing a matching rule — every bug above passed
review and failed reality.

```bash
# What does OMDb actually return for this name?
curl -s "https://www.omdbapi.com/?t=Good+Will+Hunting&y=1997&apikey=KEY"
curl -s "https://www.omdbapi.com/?s=Good+Will+Hunting&y=1997&apikey=KEY"

# What is this ID, really?
curl -s "https://www.omdbapi.com/?i=tt1013648&apikey=KEY"

# Which IDs does the search page actually contain, in order?
curl -s --compressed -A "Mozilla/5.0" "https://search.yahoo.com/search?p=imdb+Memento+2000" \
  | python -c "import sys,urllib.parse,re; h=urllib.parse.unquote_plus(sys.stdin.read()); \
    print(re.findall(r'imdb\.com/title/(tt\d+)', h)[:8])"
```

To exercise the class itself, compile it standalone with the Roslyn compiler:

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe" `
  /langversion:7.3 /target:exe /out:Harness.exe `
  /r:packages\Newtonsoft.Json.13.0.1\lib\net45\Newtonsoft.Json.dll `
  /r:System.Net.Http.dll /r:Microsoft.CSharp.dll `
  Harness.cs "MovieSelector\IMDB Scrapper.cs"
```

Two things will bite you doing that:

- `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` is the **C# 5** compiler and rejects
  `?.`. Use the Roslyn one above.
- A bare exe with no `TargetFrameworkAttribute` gets .NET Framework's *legacy* TLS defaults
  (`Ssl3, Tls`). OMDb still accepts TLS 1.0; Yahoo and DuckDuckGo do not, so they fail with
  *"Could not create SSL/TLS secure channel"* while OMDb works — which looks exactly like a
  scraper bug and is not one. Compile in an `[assembly: TargetFramework(".NETFramework,Version=v4.8.1")]`
  and copy `App.config` alongside as `Harness.exe.config`.

## Finding bad entries already in a database

An `N/A` plot in `Database\Movie_Database.xml` is a good smell test for a wrong ID — but it is
only a smell. Of 20 such entries in one library, 17 were wrong and 3 were correct films with
sparse OMDb records.

```bash
python -c "
import xml.etree.ElementTree as ET
r = ET.parse('Movie_Database.xml').getroot()
g = lambda m, t: (m.findtext(t) or '').strip()
for m in r.findall('Movie'):
    if g(m, 'Plot') == 'N/A':
        print(g(m, 'ID').ljust(12), g(m, 'Name'))
"
```

Entries that already have an ID are **not** revisited by `Rescrap Movies No ID`. To re-fetch one,
delete its `<Movie>` node from the database and rescrape.

## Known limits

- A folder named only in a non-Latin script, with no bracketed English title, cannot be
  resolved — every variant strips to nothing and the search engines return nothing usable for
  the original script. Keep the English title in brackets:
  `龙棺古墓 - 西夏狼王 (The Dragon Tomb - Ancient Legend) (2021)`.
- Two residual false-accept risks remain, both needing a wrong entry to *outrank* the correct one
  in a search engine: `Frozen 2: Burnt` (prefix rule) and, for a franchise, a different film of
  the same year (year-only pass). Both rank below the correct film in practice.
- A full library rescrape spends far more than one OMDb call per movie once the fallbacks engage.
  The shared key's 1,000/day is easy to exhaust; use your own.
