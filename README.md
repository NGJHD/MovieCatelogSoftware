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

Your key is stored in `Database\Gui_Options.xml` inside the app's own folder, so it travels with
the installation and survives version upgrades. Leaving the field empty stores nothing and falls
back to the shared key.

If OMDb refuses a request — a spent quota or a bad key — the app now says so in the notification
area instead of just reporting every movie as failed.

### Point it at your movies

In **Options**, use the **+** button to add each folder holding your movie files. The app scans them, matches each file name against IMDB, and fills in the details. Names like `Inception (2010).mkv` or `Inception.2010.mkv` match best — a year in brackets is what the matcher trusts most.

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

## Releasing

The version number lives in exactly one place: the `AssemblyVersion` / `AssemblyFileVersion` lines in
[`MovieSelector/Properties/AssemblyInfo.cs`](MovieSelector/Properties/AssemblyInfo.cs). That is the only file to edit when bumping a version.

To cut a release, bump both lines together, commit, then push a matching tag:

```
git tag v4.1.5
git push origin v4.1.5
```

The release workflow builds, checks the tag against the built `MCS.exe` version (and fails if they disagree), zips `MCS.exe` and `Newtonsoft.Json.dll`, and attaches them to a new GitHub Release. Those two files are the whole application; they must always be updated together.

Step-by-step instructions, including which files to copy when updating an existing installation, are in [RELEASE_GUIDE.md](RELEASE_GUIDE.md).

## License

[MIT](LICENSE).
