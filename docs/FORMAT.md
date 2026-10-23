# Fancy Comments format specification

**Version 1**, matching the Visual Studio extension as of October 2026.

This document defines how Fancy Comments are stored: the marker written into source code, the `FancyComments`
folder, the comment JSON files, and attached media. Any tool that reads or writes Fancy Comments, such as the
Visual Studio extension or a future VS Code extension, must follow it so that tools don't damage each other's data.

The key words **MUST**, **MUST NOT**, **SHOULD** and **MAY** are used as in [RFC 2119](https://www.rfc-editor.org/rfc/rfc2119).
Section 7 covers recommended editor behaviour, which keeps tools consistent but doesn't affect the data.

---

## 1. Overview

A Fancy Comment has three parts:

| Part | Where | Purpose |
|---|---|---|
| **Marker** | In the source file, inside a normal comment | Links the code location to the comment, and shows a one-line summary |
| **Comment JSON** | `FancyComments/Comments/<id>.json` | The full comment: Markdown body, description, metadata |
| **Media** | `FancyComments/Assets/` | Images, video, audio and other files the Markdown links to |

The **id** links them together. File paths and line numbers are only hints.

---

## 2. Comment id

- An id is exactly **8 lowercase hexadecimal characters** (`[0-9a-f]{8}`), for example `245d2313`.
- Writers MUST generate ids randomly (the Visual Studio extension uses the first 8 hex characters of a new GUID), and
  SHOULD check that `Comments/<id>.json` doesn't already exist.
- Ids are permanent. Writers MUST NOT change an existing comment's id.
- An id SHOULD appear in **at most one marker** across the solution. Two markers sharing an id share one JSON file,
  so editing one would rewrite the other and deleting one would delete the other's data. When a tool copies a
  marker, it SHOULD give the copy a new id and save a copy of the JSON under it (the media files stay shared;
  see section 6.4). Moving a marker (cut and paste) keeps its id. Readers MUST still cope with duplicates, since
  other tools and manual edits can create them.

---

## 3. The marker

### 3.1 Syntax

A marker is one line of text inside a comment, in the host language's own comment syntax:

```
<open><sep>## [fc:<id><notice>] <summary> ##<close>
```

| Element | Meaning |
|---|---|
| `<open>`, `<close>` | The language's comment opener and closer, e.g. `/*` and `*/`. `<close>` is empty for line comments (`//`, `#`, `--`, `'`). See Appendix A. |
| `<sep>` | A single space if `<open>` ends with `#`, otherwise empty. This keeps `# ##` distinct from banner comments like `#####`. |
| `<id>` | The comment id (section 2). |
| `<notice>` | Optional free text for people without the extension. It MUST NOT contain `]` or a line break. |
| `<summary>` | The one-line summary (section 3.3). |

The language-independent part, called the **marker body** below, is everything from the first `##` to the last `##`.

Examples:

```csharp
/*## [fc:245d2313 · Fancy Comment: install the Fancy Comments extension, or read FancyComments/Comments/245d2313.json] Retry uses exponential backoff ##*/
```

```python
# ## [fc:245d2313 · Fancy Comment: install the Fancy Comments extension, or read FancyComments/Comments/245d2313.json] Retry uses exponential backoff ##
```

```html
<!--## [fc:245d2313 · Fancy Comment: install the Fancy Comments extension, or read FancyComments/Comments/245d2313.json] Retry uses exponential backoff ##-->
```

### 3.2 Reading markers

Readers MUST find markers with this regular expression, applied to **one line at a time**:

```regex
##\s(?<body>\[fc:(?<id>[0-9a-f]{8})[^\]]*\]\s(?<summary>.*?))\s##
```

- A line MAY contain more than one marker, and a marker MAY follow code on the same line.
- Readers don't need to check that the marker is inside a comment. The pattern is specific enough on its own.
- The `body` group is the part an editor may hide or replace with its own display. The `##` delimiters and the
  language's comment delimiters SHOULD stay visible.

### 3.3 Writing markers

- Writers MUST write the whole marker on one line, with exactly one space after the opening `##`, between `]` and
  the summary, and before the closing `##`.
- Writers SHOULD use this notice, so that teammates without the extension know what they're looking at:
  ` · Fancy Comment: install the Fancy Comments extension, or read FancyComments/Comments/<id>.json`
- The **summary** is:
  1. the comment's `description` (section 5), trimmed, if it isn't empty; otherwise
  2. the first non-empty line of the Markdown, with HTML tags removed, links and images reduced to their text,
     leading `#`, `>`, `-` and `*` removed, and cut to 80 characters (79 plus `…`); otherwise
  3. `(empty comment)`.
- The summary MUST NOT contain a line break. Writers MUST replace every `##` in it with `#`, since `##` would end
  the marker early.
- When a comment's summary changes, writers MUST update the marker in the source and the `summary` field in the
  JSON to match.

---

## 4. The `FancyComments` folder

### 4.1 Location

The folder sits in the **root directory** for a source file: the nearest ancestor directory of that file that
contains any of:

1. a `*.sln` or `*.slnx` file,
2. a `FancyComments/Comments` directory, or
3. a `.git` directory.

The nearest match wins. If no ancestor matches, the file can't have Fancy Comments.

### 4.2 Layout

```
<root>/
└── FancyComments/
    ├── Comments/
    │   └── <id>.json      one file per comment
    └── Assets/
        └── <owner id>-<name>.<ext>
```

- Tools MUST create `FancyComments/`, `Comments/` and `Assets/` when they're first needed, and MUST NOT require
  them to exist beforehand.
- The whole folder is meant to be committed to version control alongside the code.
- `Assets/` is **managed**: files in it that no comment references may be deleted (section 6.4). Users shouldn't
  keep unrelated files there.

---

## 5. Comment JSON

`FancyComments/Comments/<id>.json`: UTF-8, indented, with camelCase property names.

```json
{
  "id": "245d2313",
  "file": "dcmanagement/userlogin.aspx.cs",
  "line": 53,
  "description": "Why login retries are rate limited",
  "summary": "Why login retries are rate limited",
  "markdown": "### Background\r\n\r\n![stonecold](Assets/245d2313-stonecold.JPEG)",
  "assets": [
    "Assets/245d2313-stonecold.JPEG"
  ],
  "created": "2026-10-21T23:29:51.914896Z",
  "modified": "2026-10-21T23:30:25.1289016Z"
}
```

| Field | Type | Required | Meaning |
|---|---|---|---|
| `id` | string | yes | The comment id. MUST equal the file name without `.json`. |
| `file` | string | yes | The source file, relative to the root directory, with `/` separators. **A hint only**: the marker is the real link. |
| `line` | number | yes | 1-based line of the marker when the comment was last saved. **A hint only**. |
| `description` | string | no | Plain text the author chose to show inline. May be empty. Comments created before descriptions existed don't have it. |
| `summary` | string | yes | The text currently written into the marker (section 3.3). |
| `markdown` | string | yes | The full comment body (section 6). May contain `\r\n` or `\n` line endings. |
| `assets` | string[] | yes | Every `Assets/…` path the Markdown references (section 6.2). |
| `created` | string | yes | ISO 8601 UTC timestamp of creation. |
| `modified` | string | yes | ISO 8601 UTC timestamp of the last save. |

Rules:

- Writers MUST rebuild `assets` from the Markdown on every save. They MUST NOT carry over old entries, or media
  that's no longer linked would stay "in use" forever.
- When writers save, they SHOULD update `file` and `line` to the marker's current location.
- Readers MUST tolerate missing optional fields and SHOULD ignore unknown fields.
- Writers SHOULD preserve fields they don't recognise. **Known limitation:** the Visual Studio extension currently
  drops unknown fields when it saves. Until that's fixed, new fields need a format version bump.

---

## 6. Markdown and media

### 6.1 Markdown

- The body is CommonMark, with common extensions (tables, task lists, strikethrough, autolinks). The Visual Studio
  extension renders it with [Markdig](https://github.com/xoofx/markdig)'s advanced extensions.
- Raw HTML is allowed. Renderers MUST NOT run scripts from it.
- Relative URLs resolve against the **`FancyComments/` folder**, so `Assets/x.png` means `FancyComments/Assets/x.png`.

### 6.2 Media references

Any text in the Markdown matching this pattern references a media file:

```regex
Assets/[^\s)"'>]+
```

After URL-unescaping (`%20` becomes a space, `%28` becomes `(`, and so on), the match is the file's path relative
to `FancyComments/`. This is deliberately broad: it matches inside image syntax, links and HTML attributes alike.
Matching too much only keeps files that could have been deleted. Matching too little would delete files that are
still in use.

### 6.3 Embedding media

Writers SHOULD insert media as follows, as a paragraph of its own (blank lines before and after). In CommonMark, a
line straight after raw HTML counts as more HTML, so media inserted there would show as plain text.

| Media | Extensions | Markdown |
|---|---|---|
| Image | `.png .jpg .jpeg .gif .svg .webp .bmp` | `![name](Assets/file)` |
| Video | `.mp4 .webm` | `<video controls src="Assets/file"></video>` |
| Audio | `.mp3 .wav .ogg .m4a` | `<audio controls src="Assets/file"></audio>` |
| Anything else | | `[name](Assets/file)` |

Writers MUST escape `(` and `)` in paths as `%28` and `%29`, because a raw `)` ends a Markdown link early.

### 6.4 Asset files

**Naming.** A new file is named `<owner id>-<name><ext>`:

- `<owner id>` is the id of the comment that first added it. It doesn't change, even if other comments link the
  file later.
- `<name>` is the original file name without its extension, with spaces replaced by `-`.
- `<ext>` is the stored file's extension. It may differ from the original's if the image was converted (section 7.4).
- If the name is taken, writers append `-2`, `-3` and so on before the extension.

**Duplicates.** Before writing a new file, writers MUST check `Assets/` for a byte-identical file. If one exists,
they MUST link to it instead of writing a copy. This applies across all comments.

**Cleanup.** Writers MAY delete files from `Assets/`, but only when **all** of these are true:

1. every JSON file in `Comments/` could be read and parsed. If any can't, nothing is deleted;
2. no comment references the file, either in its Markdown (section 6.2) or in its `assets` list;
3. the file name starts with an `<owner id>-` prefix; and
4. the owner is **known**: its JSON exists, or it's the comment that was just saved, cancelled or deleted.

Rule 4 means a file whose owner's JSON is missing is never deleted. A missing JSON may be damage (deleted by
mistake, a branch switch, an incomplete commit), not a deliberate delete. Files without an id prefix were not
created by Fancy Comments and are never deleted.

Accepted trade-off: a file shared by several comments is kept after its owner is deleted, even once nothing links it.

---

## 7. Recommended editor behaviour

None of this affects the stored data, but following it keeps tools consistent.

### 7.1 Creating a comment

- Typing a language's **trigger** (`<open><sep>##`, for example `/*##`, `//##`, `# ##` or `<!--##`) opens the
  comment editor.
- On save, the trigger text is replaced with the full marker. On cancel, it's left as typed.
- Files whose extension isn't in Appendix A don't get a trigger.

### 7.2 Displaying a comment

- The marker body is replaced by the summary plus actions to **read**, **edit** and **delete**.
- **Missing data:** if `Comments/<id>.json` doesn't exist, the comment is shown as damaged. Show a warning with the
  text *"Comment data missing (FancyComments/Comments/&lt;id&gt;.json). It may not be committed, or was deleted."*
  and disable read, edit and delete. Tools MUST NOT automatically recreate or delete anything for a comment whose
  data is missing.

### 7.3 Copying and removing markers

- **Copy and paste**, or any other edit that duplicates a marker within a file: separate the copy as section 2
  describes. Undo and redo SHOULD be left alone, so they restore exactly what was there.
- **Markers deleted by hand**: their JSON and media stay. Tools MAY offer a **manual** cleanup that scans the
  solution for markers and deletes comments no marker uses. It MUST list the comments and ask before deleting,
  MUST read unsaved editor contents or save them first, and MUST delete nothing if any file can't be read. It
  MUST NOT run automatically, because a branch switch can make a marker look deleted when it isn't.

### 7.4 Attaching media

Tools SHOULD shrink images before storing them (the user may turn this off):

- Applies to `.png .jpg .jpeg .bmp .tif .tiff`. GIF (may be animated), SVG and all other files are stored unchanged.
- Apply EXIF orientation first. Re-encoding drops the tag, and phone photos would end up sideways.
- Scale images wider than **1600 px** down to 1600 px wide.
- JPEG input becomes JPEG at quality **85**. Other input stays PNG if any pixel is transparent. Otherwise it becomes
  JPEG only if that's under half the size of the PNG.
- If the image didn't need scaling and re-encoding saves less than 10%, keep the original bytes.
- Optimizing the same input MUST give the same output bytes, so the duplicate check (section 6.4) still works.

Tools SHOULD ask for confirmation before storing any file over **1 MB** (after optimizing), because the repository
keeps every committed version forever.

### 7.5 Rendering

- Serve the Markdown with `FancyComments/` as the base, so `Assets/…` resolves.
- Turn scripts off.
- Open web links in the user's browser and `Assets/…` links with the system's default app, never inside the preview.

---

## Appendix A: Comment syntax by file type

Where several syntaxes are listed, the first is preferred when writing. Readers accept markers in any of them.

| Extensions | Syntaxes (`open` … `close`) | Trigger |
|---|---|---|
| `.cs .c .h .cpp .hpp .cc .java .kt .scala .swift .go .rs .js .jsx .ts .tsx .dart .scss .less` | `/*` … `*/`, `//` | `/*##`, `//##` |
| `.css` | `/*` … `*/` | `/*##` |
| `.fs .fsx` | `(*` … `*)`, `//` | `(*##`, `//##` |
| `.py .ps1 .psm1 .sh .bash .rb .pl .r .yml .yaml .toml .cmake`, `Dockerfile` | `#` | `# ##` |
| `.vb` | `'` | `'##` |
| `.sql` | `/*` … `*/`, `--` | `/*##`, `--##` |
| `.lua` | `--` | `--##` |
| `.bat .cmd` | `::` | `::##` |
| `.html .htm .xml .xaml .md .csproj .props .targets .config` | `<!--` … `-->` | `<!--##` |
| `.razor .cshtml` | `@*` … `*@`, `<!--` … `-->` | `@*##`, `<!--##` |

Matching is case-insensitive. `Dockerfile` matches the whole file name.

---

## Appendix B: Changes

| Version | Changes |
|---|---|
| 1 | First version, documenting the Visual Studio extension. |

The format has no version field yet. A change that older tools can't handle safely, such as a new required field
or a new marker syntax, should add a `formatVersion` field to the JSON and bump the version here.
