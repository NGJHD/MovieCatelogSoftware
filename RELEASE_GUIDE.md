# Release Guide

How to build MCS and publish a release. Follow it top to bottom; nothing is assumed.

---

## Part 0 — One-time setup

You need these installed once:

- **Visual Studio 2022** (Community edition is free) with the **.NET desktop development** workload ticked during install.
- **.NET Framework 4.8.1 targeting pack** — Visual Studio Installer → Modify → Individual components → search "4.8.1" → tick *.NET Framework 4.8.1 SDK* and *targeting pack*.
- **Git**.

That's it. NuGet comes with Visual Studio.

---

## Part 1 — Build and test locally

### 1.1 Open and build

1. Open `MovieSelector.sln` in Visual Studio.
2. In the toolbar, change the dropdown that says **Debug** to **Release**.
3. Menu: **Build → Rebuild Solution**.

Wait for the Output window to say `Rebuild All: 1 succeeded`.

If it fails with a missing `Newtonsoft.Json`, right-click the solution in Solution Explorer → **Restore NuGet Packages**, then rebuild.

### 1.2 Find the output

Everything lands in:

```
MovieSelector\bin\Release\
```

### 1.3 Test it before releasing

Run `MovieSelector\bin\Release\MCS.exe` and check:

- The window opens.
- Options (gear icon) shows your movie folders and the OMDb API key.
- Pick a movie and press **Refresh** next to the IMDB ID — details should come back, not an error.

If something misbehaves, look in `MovieSelector\bin\Release\Log\` — every error is written there with a timestamp.

---

## Part 2 — Which files actually ship

`bin\Release\` contains more than you need. Only these **two** go out:

| File | Ship it? | Why |
|---|---|---|
| `MCS.exe` | **Yes** | The application. |
| `Newtonsoft.Json.dll` | **Yes** | JSON library. **The exe will not run without it.** |
| `MCS.exe.config` | No | Carries no setting the app depends on. Verified: it runs fine without it. |
| `MCS.pdb` | No | Debug symbols. Only useful for debugging with line numbers. |
| `Newtonsoft.Json.xml` | No | IntelliSense documentation for developers. Never read at runtime. |
| `Database\` `Posters\` `Log\` | No | Your own data. The app creates empty ones on first run. |

> **The single most important rule:** `MCS.exe` and `Newtonsoft.Json.dll` must always be updated
> **together**. The exe records the exact Newtonsoft version it needs. Up to v4.1.5 it asked for
> `12.0.0.0`; it now asks for `13.0.0.0`. Copying only the exe over an install still holding the
> old DLL is what crashed the app on 30 Aug 2026.

The release workflow packages exactly the two files above, so if you use Part 4 you get this right automatically.

---

## Part 3 — Updating your own installation

Your install lives at `Y:\Movies & Dramas\Movies\Movie Selector\`.

> ### Never copy the whole `bin\Release\` folder over your installation
>
> The moment you test-run `bin\Release\MCS.exe`, it creates its own **empty** `Database\`,
> `Posters\` and `Log\` folders right there. Copying the folder wholesale overwrites your real
> `Movie_Database.xml` and `Gui_Options.xml` with those empty ones — your entire catalogue and
> your OMDb key, gone, with no error message and nothing in the log. This is exactly how ~385
> entries were destroyed on 30 Aug 2026.
>
> **Copy the three files individually. Never the folder.**

**The easy way:** double-click **`COPY TO NETWORK.bat`** in the repository root. It copies the two
files, refuses to run if MCS is still open or the Y: drive is unreachable, and never touches your
`Database\`, `Posters\` or `Log\` folders.

By hand:

1. Close MCS if it is running.
2. Copy **exactly these two files** from `MovieSelector\bin\Release\` into that folder, overwriting:
   - `MCS.exe`
   - `Newtonsoft.Json.dll`
3. Start MCS.

Never copy over, or delete, `Database\`, `Posters\` or `Log\` — that is your catalogue, your
posters and your settings, including your OMDb key.

> There is no automatic backup of `Movie_Database.xml`. If you want one, copy the `Database\`
> folder somewhere safe before you experiment.

---

## Part 4 — Publishing a release on GitHub

GitHub builds and packages for you. You only push a tag.

### 4.1 Decide the version number

The version lives in **exactly one file**:

```
MovieSelector\Properties\AssemblyInfo.cs
```

Near the bottom:

```csharp
[assembly: AssemblyVersion("4.1.5.0")]
[assembly: AssemblyFileVersion("4.1.5.0")]
```

**Change both lines to the same new number.** For example, going to 4.1.6:

```csharp
[assembly: AssemblyVersion("4.1.6.0")]
[assembly: AssemblyFileVersion("4.1.6.0")]
```

Rough guide: bug fix → bump the third number (4.1.5 → 4.1.6). New feature → bump the second (4.1.5 → 4.2.0).

### 4.2 Commit the version bump

In a terminal, from the repository folder:

```
git add MovieSelector/Properties/AssemblyInfo.cs
git commit -m "Bump version to 4.1.6"
git push
```

### 4.3 Tag and push the tag

The tag must be `v` followed by the first three numbers of the version:

```
git tag v4.1.6
git push origin v4.1.6
```

> The release workflow compares the tag against the version compiled into `MCS.exe` and **fails the release if they disagree**. That is deliberate — it stops a mislabelled download reaching anyone.

### 4.4 Watch it run

1. Go to your repository on GitHub → the **Actions** tab.
2. You will see a run called **Release** for your tag. It takes a few minutes.
3. Green tick = done. Red cross = click into it to read which step failed.

### 4.5 Check the result

Go to the **Releases** page of the repository. There will be a new release with `MCS-v4.1.6.zip` attached. Download it, unzip it somewhere fresh, and run `MCS.exe` to confirm it works.

---

## Part 5 — If something goes wrong

**"Tag v4.1.6 does not match AssemblyVersion 4.1.5.0"**
You tagged without bumping `AssemblyInfo.cs`, or bumped only one of the two lines. Fix the file, commit, then move the tag:

```
git tag -d v4.1.6
git push origin :refs/tags/v4.1.6
git tag v4.1.6
git push origin v4.1.6
```

**The build fails on GitHub but works on your machine.**
Almost always a file you never committed. Check `git status` for untracked files that the build needs.

**You published a broken release.**
Go to the release on GitHub → **Delete**. Then delete the tag with the commands above, fix the problem, and start again from 4.1.

---

## Quick reference

```
# build            : Visual Studio -> Release -> Build -> Rebuild Solution
# output           : MovieSelector\bin\Release\
# ship             : MCS.exe + Newtonsoft.Json.dll        (always both, never one alone)
# deploy to Y:      : double-click "COPY TO NETWORK.bat"
# version file     : MovieSelector\Properties\AssemblyInfo.cs   (both Version lines)
# publish          : git tag v<version> && git push origin v<version>
# your OMDb key    : Database\Gui_Options.xml, in the install folder
```
