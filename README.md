# Movie Catelog Software

Sometimes, we rent/bought and ripped so much movies to store in our HDD that we have difficulty tracking what's what. Maybe you can remember what's each movie about, but not your family.

So I wrote a lightweight software (it's just 800+ KB) for Windows some years back for my dad. It's a simple software that grabs the movie details from IMDB so that he can filter the nice ones to watch. I am aware that there are existing implementations in the market (namely kodi), but those are bloated, slow and not elderly friendly.

P.S
This is not a software to help you download movies. This is a software that helps you grab information about the movie (movies that you already have) from the internet.

<img width="1920" height="1040" alt="image" src="https://github.com/user-attachments/assets/6c75dfe9-2fd7-44ca-87b2-abff85beb444" />

## Getting started

### Install

Grab the latest zip from [Releases](../../releases), unzip it anywhere, and run `MCS.exe`. Nothing is installed and nothing is written outside the folder you unzip into — the app keeps its database, posters and logs in `Database\`, `Posters\` and `Log\` next to the executable.

### You need a free OMDb API key

Movie details come from [OMDb](https://www.omdbapi.com/), which requires a key. The app ships without one, so the first thing to do is get your own:

1. Request a free key at <https://www.omdbapi.com/apikey.aspx> (the FREE tier allows 1,000 lookups per day).
2. Activate it from the email OMDb sends you.
3. Open **Options** in the app (the gear icon), paste the key into **OMDb API Key**, and press **SAVE**.

The key is stored in `Database\Gui_Options.xml` inside the app's own folder, so it travels with the installation and survives version upgrades. It is never committed to this repository. Without a key the catalogue still lists your files, but no ratings, plots or posters can be fetched.

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

The release workflow builds, checks the tag against the built `MCS.exe` version (and fails if they disagree), zips `MCS.exe`, `MCS.exe.config` and `Newtonsoft.Json.dll`, and attaches them to a new GitHub Release.

Step-by-step instructions, including which files to copy when updating an existing installation, are in [RELEASE_GUIDE.md](RELEASE_GUIDE.md).

## License

[MIT](LICENSE).
