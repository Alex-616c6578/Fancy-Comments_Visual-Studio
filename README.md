# Fancy Comments

<p align="center">
  <img src="Assets/FancyCommentsMascot.png" alt="The Fancy Comments mascot: a dashed-line stick figure tipping a top hat, wearing a monocle and carrying a cane" width="220">
</p>

A Visual Studio extension for writing rich comments in code: Markdown with images, video, audio and links, shown
in the editor as a single line you can open.

```csharp
public async Task<bool> LoginAsync(string user, string password)
{
    /*## Why login retries are rate limited 👁 ✏ 🗑 ##*/
    ...
```

Type `/*##` and an editor opens with Markdown on the left and a live preview on the right. When you save, the
comment collapses to one line in your code, with buttons to read, edit or delete it.

## Features

- **Rich content**: Markdown with tables, code blocks, images, video, audio and links to wikis or files.
- **Works in most languages**: C#, C/C++, JavaScript/TypeScript, Python, PowerShell, VB, SQL, HTML/XAML and more.
  Each uses its own comment syntax, so the code still compiles.
- **Stored in your repository**: comments live in a `FancyComments` folder next to your solution and are committed
  with the code, so the whole team gets them.
- **Readable without the extension**: teammates who don't have it see a normal comment with the summary, a note
  about the extension, and the path to the comment's file.
- **Keeps the repository small**: images are scaled down and re-compressed, identical files are stored once,
  unused media is removed, and you're warned before committing anything over 1 MB.
- **Safe with missing data**: if a comment's data is missing, for example deleted or not committed, the comment is
  flagged in the editor and nothing is deleted.

## Using it

### Create a comment

In a code file, type the trigger for that language:

| Language | Type |
|---|---|
| C#, C/C++, Java, JavaScript/TypeScript, Go, Rust, CSS… | `/*##` or `//##` |
| Python, PowerShell, shell, Ruby, YAML… | `# ##` (note the space) |
| VB | `'##` |
| SQL | `/*##` or `--##` |
| HTML, XML, XAML, Markdown, project files | `<!--##` |
| Razor | `@*##` or `<!--##` |

The full list is in [docs/FORMAT.md](docs/FORMAT.md#appendix-a-comment-syntax-by-file-type).

In the editor that opens:

- **Description**: the plain-text line shown in your code. If you leave it empty, the first line of the Markdown is
  used.
- **Markdown**: the full comment, with a live preview alongside.
- **Insert media…**: attach an image, video, audio file or document. It's copied into `FancyComments/Assets` and
  linked in the Markdown.
- **Insert link**: adds a Markdown link, for wiki pages for example.
- **Optimize images** (on by default): scales images wider than 1600 px down and re-compresses them. Untick it to
  attach the original file.

Press **Save** to insert the comment, or **Cancel** to leave what you typed as it is.

### Read, edit or delete

Each comment shows its description in the line, with three buttons:

| Button | Does |
|---|---|
| 👁 Read | Opens the rendered comment in a window |
| ✏ Edit | Reopens the editor |
| 🗑 Delete | After you confirm: removes the comment from the code, deletes its data, and deletes any media no other comment uses |

Ctrl+Z after deleting brings back the text in your code, but not the deleted files. Restore those from git if needed.

### Preview on hover

With hover previews turned on, rest the mouse on a comment's description and the rendered comment appears below
it. Move the mouse into the preview to scroll or click a link; it closes when the mouse leaves both. Turn it on or
off with **Extensions → Fancy Comments → Preview Comments on Hover** (off by default). When it's off, hovering
shows a tooltip with the first 100 or so characters of the comment as plain text instead.

The hover preview is drawn by Visual Studio itself rather than a browser, so it opens instantly and nothing stays
running afterwards. Video, audio, SVG and WebP need a browser, so they show as links that open the file; use
👁 Read to see them in place. Images on the web aren't fetched on hover.

### Copy, move and delete comments in the code

Edit the code normally:

- **Cut and paste** moves a comment.
- **Copy and paste**, or duplicating a line, makes an independent copy. It gets its own ID and its own copy of the
  comment's data, so editing or deleting one doesn't affect the other. Media files are shared, not copied.
- **Deleting the comment's text** (or the code around it) removes it from the code. Its data and media stay, so
  Ctrl+Z brings it back. To delete those for good, use the 🗑 button, or the cleanup command below.

### Clean up unused comments

**Extensions → Fancy Comments → Clean Up Unused Fancy Comments…** scans the solution for comments whose text no longer appears in
any code file, lists them, and deletes them (with any media no other comment uses) after you confirm. It asks to
save open files first, and deletes nothing if any file can't be read.

Run it from the branch everyone works from. A comment that only exists on another branch looks unused here, and
deleting it would make it show as missing data there after a merge.

### If a comment shows a warning

```
/*## ⚠ Retry uses exponential backoff — Comment data missing (FancyComments/Comments/1a2b3c4d.json). It may not be committed, or was deleted. 👁 ✏ 🗑 ##*/
```

The comment's data file is missing. Usually it hasn't been committed or pulled yet, or it was deleted by mistake.
The buttons are disabled so nothing makes it worse. To fix it:

- restore the file from git: `git checkout -- FancyComments/`, or ask whoever wrote the comment to commit it; or
- if the comment isn't wanted, delete its line from the code by hand. Its media is kept, since the extension can't
  tell whether anything else uses it.

The warning clears when the line is next redrawn, for example by scrolling or reopening the file.

## What gets committed

```
YourSolution/
├── YourSolution.sln
└── FancyComments/
    ├── Comments/
    │   └── 245d2313.json                 the comment: Markdown, description, metadata
    └── Assets/
        └── 245d2313-architecture.png     media the comment links to
```

Commit the `FancyComments` folder together with the code changes that add the comments. Each comment in the code
is a single line, for example:

```csharp
/*## [fc:245d2313 · Fancy Comment: install the Fancy Comments extension, or read FancyComments/Comments/245d2313.json] Why login retries are rate limited ##*/
```

Don't keep other files in `FancyComments/Assets`. The extension manages that folder, and files there that no
comment uses get deleted.

The exact format, and the rules any tool editing these files must follow, are in
**[docs/FORMAT.md](docs/FORMAT.md)**.

### Repository size

Git keeps every committed file forever, even after it's deleted. To keep that in check, the extension shrinks
images, stores identical files once, and asks before adding anything over 1 MB. Video and audio aren't compressed,
so keep clips short. If your team attaches a lot of media, consider [Git LFS](https://git-lfs.com/) for the assets:

```gitattributes
FancyComments/Assets/** filter=lfs diff=lfs merge=lfs -text
```

Everyone on the team needs Git LFS installed for this.

## Requirements

- Visual Studio 2022 version 17.14 or later, including Visual Studio 2026 (version 18).
- The Microsoft Edge WebView2 Runtime, which is included with Windows 10 and 11.

## Building and debugging

1. Install the **Visual Studio extension development** workload with the Visual Studio Installer.
2. Open `FancyComments.slnx`.
3. Press **F5**. The extension is built and installed into the *experimental instance*, a separate copy of
   Visual Studio with its own settings, which then opens.
4. In the experimental instance, open a **different** solution to test with (see Known limitations).

If F5 says deployment isn't enabled, open **Build → Configuration Manager** and tick **Deploy** for FancyComments.

### Tests

`FancyComments.Tests` is an MSTest project covering everything that doesn't need Visual Studio running: markers and
summaries, comment syntaxes, the store and its cleanup rules, the solution scan, image optimizing, media
embedding and the hover preview's Markdown rendering. Run it from **Test → Test Explorer**, or from a Developer
Command Prompt:

```
msbuild FancyComments.Tests -restore
vstest.console FancyComments.Tests\bin\Debug\net472\FancyComments.Tests.dll
```

Image tests generate their pictures and run on an STA thread (`TestImages.Sta`), which WPF imaging requires. Store
tests run against a throwaway solution folder (`TempSolution`).

The editor integration (inline previews, the typing trigger, copy and paste, the dialogs) needs a running
Visual Studio, so it isn't covered by these tests. Check it by hand with F5.

On GitHub, `.github/workflows/ci.yml` builds the solution and runs the tests on every push and pull request, and
attaches the built `.vsix` and test results to each run.

### Linting

Code style lives in `.editorconfig`, and Visual Studio shows violations as you type. Every build reports them too,
and CI fails on any warning. To check before pushing:

```
dotnet format whitespace FancyComments.slnx --verify-no-changes
msbuild FancyComments.slnx -restore -warnaserror
```

`dotnet format whitespace FancyComments.slnx` fixes formatting for you.

### Project layout

| Path | Contents |
|---|---|
| `FancyComments/Model/` | Storage and format: markers (`CommentMarker`), languages (`CommentLanguages`), the comment store and media cleanup (`CommentStore`), the solution scan for used comments (`CommentScanner`), image optimization (`ImageOptimizer`) |
| `FancyComments/Editor/` | Editor integration: typing trigger (`TriggerListener`), inline previews (`FancyCommentTagger`, `CommentPreview`), the read, edit and delete actions (`FancyCommentController`), the hover preview timing (`HoverPreview`), and keeping IDs unique on copy and paste (`DuplicateGuard`, `ClipboardCommandHandler`) |
| `FancyComments/UI/` | Windows: the comment editor dialog, the read-only viewer, the WebView2 Markdown preview, the hover preview window and its native Markdown renderer (`HoverPreviewWindow`, `MarkdownDocument`), and message boxes (`Prompt`) |
| `FancyComments/CleanupCommand.cs` | The Clean Up Unused Fancy Comments command |
| `FancyComments/HoverPreviewCommand.cs`, `ExtensionSettings.cs` | The Preview Comments on Hover toggle and where it's saved |
| `FancyComments.Tests/` | Unit tests |
| `docs/FORMAT.md` | The storage format specification |

The project is a VisualStudio.Extensibility extension with VSSDK compatibility, running in-process, because the
inline previews need the in-process editor API.

## To Do

- **Copies across Visual Studio windows**: a comment copied in one Visual Studio window and pasted into another
  keeps the same ID. If that happens, delete the pasted copy and paste it again in the same window.
- **Media shared between comments**: a file is named after the comment that first added it. If that comment is
  deleted, the file is kept even once no comment links it.
- **Warnings refresh on redraw**: after a missing data file is restored, the warning clears the next time that line
  is redrawn, not immediately.
- **Unknown JSON fields are dropped** when a comment is saved (see [FORMAT.md §5](docs/FORMAT.md#5-comment-json)).
