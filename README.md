# Movie Catelog Software

> ## ⚠️ Please get your own free OMDb API key
>
> **MCS ships with a shared OMDb key so it works the moment you unzip it. That key is used by
> everyone who never sets their own, so its 1,000 lookups a day get exhausted quickly — often
> within minutes. When it runs out, fetching stops working for everybody until the next day.**
>
> **Getting your own takes about a minute and is free:**
>
> 1. **Request a key at <https://www.omdbapi.com/apikey.aspx> (select the FREE tier).**
> 2. **Click the activation link OMDb emails you — the key does not work until you do.**
> 3. **In MCS, open Options (the gear icon), paste it into "OMDb API Key", and press SAVE.**
>
> **A key of your own gives you your own 1,000 lookups a day, which is plenty for a large
> library. Leave the box empty to go back to the shared key.**

Sometimes, we rent/bought and ripped so much movies to store in our HDD that we have difficulty tracking what's what. Maybe you can remember what's each movie about, but not your family.

So I wrote a lightweight software (it's just 800+ KB) for Windows some years back for my dad. It's a simple software that grabs the movie details from IMDB so that he can filter the nice ones to watch. I am aware that there are existing implementations in the market (namely kodi), but those are bloated, slow and not elderly friendly.

P.S
This is not a software to help you download movies. This is a software that helps you grab information about the movie (movies that you already have) from the internet.

<img width="1920" height="1040" alt="image" src="https://github.com/user-attachments/assets/6c75dfe9-2fd7-44ca-87b2-abff85beb444" />

## Getting started

### Install

Grab the latest zip from [Releases](../../releases), unzip it anywhere, and run `MCS.exe`. Nothing is installed and nothing is written outside the folder you unzip into — the app keeps its database, posters and logs in `Database\`, `Posters\` and `Log\` next to the executable.

### The OMDb API key

Movie details come from [OMDb](https://www.omdbapi.com/), which requires a key. A shared key is
built in so the app works straight away, but see the notice at the top — it is shared, so it runs
out. Setting your own is strongly recommended.

A first scan of a large library costs more than one lookup per movie: a name OMDb cannot match
directly is resolved through a web search and then verified, which spends several. The shared
key's 1,000 a day does not go far against a few hundred films.

Your key is stored in `Database\Gui_Options.xml` inside the app's own folder, so it travels with
the installation and survives version upgrades. Leaving the field empty stores nothing and falls
back to the shared key.

If OMDb refuses a request — a spent quota or a bad key — the app now says so in the notification
area instead of just reporting every movie as failed.

### Keeping it up to date

**Options → Software Update → CHECK FOR UPDATE** asks GitHub what the newest published release
is. If it is newer than the running build, MCS offers to install it: it downloads the release
zip, closes, replaces `MCS.exe` and `Newtonsoft.Json.dll` together, and starts itself again.

Nothing under `Database\`, `Posters\` or `Log\` is touched, so your catalogue, posters and OMDb
key survive the upgrade. If either file cannot be replaced the update is abandoned with the old
version left intact, and what happened is written to `Log\MCS_update.log`.

The check only ever runs when you press the button — MCS does not phone home on startup. The
repository must be public for it to work, since the check is an anonymous GitHub API call.

### Point it at your movies

In **Options**, use the **+** button to add each folder holding your movie files. The app scans them, matches each file name against IMDB, and fills in the details.

**Include the year.** It is what the matcher trusts most, and it doubles as the cut-off point for
everything a release group appends, so all of these resolve to the same film:

```
Inception (2010).mkv
Inception.2010.mkv
Inception.2010.1080p.BluRay.x264-SPARKS.mkv
```

Without a year the name still works, but a film that shares its title with another is far more
likely to come back wrong.

A sequel numbered the way IMDB does not — `John Wick 3`, `Frozen 2`, `Mission Impossible 6` — is
matched through a web search rather than directly, so it takes a little longer.

**For a film named in another script, keep an English title in brackets:**

```
龙棺古墓 - 西夏狼王 (The Dragon Tomb - Ancient Legend) (2021)
```

Neither OMDb nor the search engines find anything from the original script alone, so a folder
named only in Chinese, Japanese or Korean cannot be matched.

## Building from source

**Prerequisites**

- Windows
- Visual Studio 2022 (Community is fine) with the **.NET desktop development** workload
- .NET Framework 4.8.1 targeting pack

**In Visual Studio**

Open `MovieSelector.sln` and build. NuGet restores `Newtonsoft.Json` automatically on first build.

**From the command line**

```
nuget restore MovieSelector.sln
msbuild MovieSelector.sln /t:Rebuild /p:Configuration=Release
```

The executable lands in `MovieSelector\bin\Release\MCS.exe`.

Every push and pull request is built from a clean clone by [the build workflow](.github/workflows/build.yml), so a reference that only resolves on one machine fails CI rather than reaching you.

Before changing how file names are matched to IMDB entries, read [DEBUG.md](DEBUG.md). It records
what each rule in the matcher is defending against, with the wrong IDs that got written when the
rule was not there.

## Releasing

The version number lives in exactly one place: the `AssemblyVersion` / `AssemblyFileVersion` lines in
[`MovieSelector/Properties/AssemblyInfo.cs`](MovieSelector/Properties/AssemblyInfo.cs). That is the only file to edit when bumping a version.

To cut a release, bump both lines together, commit, then push a matching tag:

```
git tag v4.1.5
git push origin v4.1.5
```

The release workflow builds, checks the tag against the built `MCS.exe` version (and fails if they disagree), zips `MCS.exe` and `Newtonsoft.Json.dll`, and attaches them to a new GitHub Release. Those two files are the whole application; they must always be updated together.

To update an existing installation by hand, copy **only** those two files over it. Never copy the
whole `bin\Release\` folder: test-running the build creates empty `Database\`, `Posters\` and
`Log\` folders beside it, and copying the folder wholesale overwrites a real catalogue and OMDb
key with those empty ones, silently. The in-app updater does this correctly on its own.

## License

[MIT](LICENSE).
