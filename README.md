# DM42 / C47 Editor

**English** | [Français](README.fr.md)

A Windows editor for programs of the **SwissMicros DM42**, the **HP-42S**, the **Free42 / Plus42** emulators, and the **C47 / R47**.
Write RPN with highlighting and help, draw its flowchart, step through it in a simulator, or write it in Python and let
the editor compile it to RPN.

**Download**: the Windows installer is in the [Releases](https://github.com/philgerm57/DM42Editor/releases)
(also on [GitLab](https://gitlab.com/philgerm57/DM42Editor)). Feedback and bugs: [issues](https://github.com/philgerm57/DM42Editor/issues).

![RPN program editor](docs/editeur.png)

## Features

- **RPN editing** with highlighting (functions, numbers, strings, tests, comments), line numbers, bookmarks, find / replace, printing, light or dark theme.
- **Encoding / decoding** of DM42 `.raw` files, with a byte-by-byte viewer (line, RPN code, comment, bytes, error).
- **Flowchart** in the style of the HP-41C programming sheets: diamonds for tests (Y / N), instruction blocks, `XEQ` calls with a double bar,
  input / output as parallelograms, a comment column. Updated while you type.
  It also draws C47 code (Ctrl+L on a C47 tab): `IF` / `ELSE` / `ENDIF`, `DO` / `WHILE` / `ENDDO`, `REPEAT` / `UNTIL`,
  `FOR` / `NEXT` (test at the bottom or at the top of the loop), C47 tests, relative jumps `SKIP` / `BACK`, `FINISH`.
- **Simulator under the RPN editor** (F5, see [Program simulator](#program-simulator)): a DM42 simulator with step by step
  and breakpoints on a DM42 tab, the real C47 simulator with the calculator screen on a C47 tab.
- **HP-41C → DM42 conversion** of the syntax (stack registers, two-digit arguments, etc.).
- **Python → RPN** (Py42): write the program in Python, get the HP-42S listing and simulate it before sending it.
  With the **C47 / R47** target (*Target* list of the Python window), the program is made for the C47:
  `IF` / `DO` / `FORᵀᴼᴾ` structures instead of `GTO`, local variables in `LocR` registers (recursion allowed),
  HP-42S names converted (`42α`, `42ARCL`, `42AVIEW`, `42KEY`…). F5 runs it in the C47 simulator, F6 opens it in a
  C47 tab, F8 saves it as .p47, F7 sends it to the C47 emulator.
  *C47 / R47 examples → Programs from the C47 distribution*: twelve programs of the simulator's `PROGRAMS` folder
  (BMI, NQueens, PAYCALC, FreeF, Bairsto, STRUCT…) rewritten in Python.
- **C47 RPN**: C47 tabs in the same editor (*File > New C47 program*, or open a `.p47` / `.p47u`); highlighting, errors shown
  while typing, instructions listed by topic (also in *Help > C47 instructions*), including the non-programmable keys and menus
  of the C47 index card (`EEX`, `+ZOOM` / `-ZOOM`, `SST`, `LOADP`…), marked [key] and never inserted;
  *Encode and save as .p47* and a byte viewer (see [C47/README.md](C47/README.md), in French).
- **Typing help**: tooltip on the word under the mouse (instruction, register `R07`, flag `F01`, number, remark…),
  autocompletion from two letters with a short description, and search by name or description above the function list.
  For the C47: a symbol bar (→ ⇄ ≤ ≥ ≠ × ÷ ² ˣ √ π…), replacement while typing (`->` → `→`, `<>` → `⇄`, `<=` → `≤`,
  `^2` → `²`, except in strings and comments), and keyboard spellings accepted (`x<>y`, `abs`, `STO*`).
- **Send to the emulator** Free42 / Plus42 (F7), whose path is set in the settings.
- Built-in help: the HP-42S function list and the PDF manuals in a WebView2 viewer.

| Flowchart | Python → RPN |
|---|---|
| ![Flowchart](docs/logigramme.png) | ![Python window](docs/python.png) |

## Program simulator

*File → Program simulator* (Ctrl+F5) shows or hides, under the editor, the same simulator as the Python window;
**F5** (*File → Run the simulation*) opens it and runs the program. Choose the global label to run, the values put on the
**stack** (the last one goes to X) and the answers of the **INPUT** field, in order: `INPUT` / `PROMPT`,
one value per variable of a `VARMENU`, the key number of a `MENU` (1 to 6, 0 = EXIT), the key code of
`GETKEY` and `KEY?`. When you change tabs, the simulator takes the labels and the type (DM42 or C47) of the new tab.

### DM42 tab

The listing (step numbers, `{ … }` header and comments ignored) is run by the Py42 simulator, in double
precision (16 digits, instead of 34 on the DM42). Besides the code produced by Py42, it handles:

- the stack (`ENTER`, `R↑`, `X<>Y`…), numbered registers (`RCL 05`, `STO+ 12`), `ISG` / `DSE`, `GTO "NAME"`, local `XEQ`;
- **indirect addressing** `IND` (`RCL IND 05`, `STO IND "N"`, `GTO IND 00`, `XEQ IND "F"`, `ISG IND 00`, `FIX IND ST X`…)
  and the **stack registers** `ST X` to `ST T` (`RCL ST Y`, `STO+ ST Z`, `X<> ST T`, `VIEW ST Y`);
- the **solver** `PGMSLV` / `SOLVE`: guesses in the variable and in X, as on the calculator; X = root,
  Y = previous estimate, Z = f(root), T = code (0 root, 1 sign reversal, 2 extremum, 3 bad guesses,
  4 constant); without a root, the next line is skipped;
- **integration** `PGMINT` / `INTEG` from `LLIM` to `ULIM` with the accuracy `ACC`: X = integral, Y = uncertainty.

An error puts the cursor on its line. `ST L` (LASTX) is not simulated.

**Step by step** (Visual Studio keys, also in *File → Step by step* and in the simulator bar):

| Key | Action |
|---|---|
| F9 | Sets or clears a breakpoint on the caret line (red dot in the margin); Ctrl+Shift+F9 clears them all |
| F5 | Runs the program to the first breakpoint; while stepping, continues to the next one or to the end |
| F11 | Step into: next instruction, entering `XEQ` calls and the function of `SOLVE` / `INTEG` |
| F10 | Step over: an `XEQ`, `SOLVE` or `INTEG` runs in one go |
| Shift+F11 | Step out: until the current subroutine returns (`RTN`) |
| Ctrl+F10 | Step back: goes back one instruction |
| Shift+F5 | Leaves step by step |

The next instruction is highlighted in the editor, with an arrow in the margin; the simulator shows the T Z Y X stack,
ALPHA, the variables, the registers, the messages already shown and the call level. The program is run once, recording
the state before each instruction: since the inputs are given in advance, this is the same as running it instruction
by instruction, and you can also go back. Recording stops after 200,000 steps; a modified program is restarted from the
beginning. Matrices are shown by their dimensions.

### C47 tab

The program is encoded, then run by the real C47 simulator without a window (`c47.exe --headless`), on 34 digits.
The simulator shows the displayed messages, the 8-level stack (readable exponents: `TEXT²`, `×10⁻³`) and
the calculator screen (click it to enlarge). The window stays usable during the simulation; F5, Shift+F5 or the
Stop button interrupt it, and it stops by itself after 30 s.

The saved or sent program is not changed: a copy is simulated, prepared for this simulator without a keyboard
(`C47/C47.Core/SimCopy.cs`):

- the messages of `42AVIEW`, `42PRMPT` and `VIEW` are kept in a log, read at the end;
- a `MENU` followed by `STOP` asks for the key number, a `VARMENU` for one value per variable;
- waiting for a key (`KEY? r`, `GETKEY`) captures the screen drawn by the program, then reads the key code from
  the INPUT field; without an answer, the program stops there instead of waiting forever (MOON47);
- a `PAUSE` just before the end (`PAUSE 99`, "look at the screen") captures the screen (GRFNTS); the other `PAUSE`s are removed;
- `END` becomes `RTN`, the return it makes on the calculator.

Step by step exists only for the DM42: the C47 simulator runs in one go. Only the first screen capture is kept.

## Building

Requirements: Windows, the [.NET 10 SDK](https://dotnet.microsoft.com/download), the WebView2 runtime (included in Windows 11).

The project relies on two sibling repositories, expected in the same parent folder:

```
dm42_editor/
├── dm42editor/   ← this repository
├── Py42/         ← Python → RPN compiler (Py42.Core)
└── dm42dll/      ← .raw encoder / decoder (.NET Framework 4.7.2)
```

1. Clone the three repositories side by side (`Py42` with a capital P, the name the project expects):

   ```bash
   mkdir dm42_editor && cd dm42_editor
   git clone https://github.com/philgerm57/DM42Editor.git dm42editor
   git clone https://github.com/philgerm57/py42.git Py42
   git clone https://github.com/philgerm57/dm42dll.git dm42dll
   ```

2. Build **dm42dll first**: dm42editor uses `..\dm42dll\bin\Debug\dm42dll.dll`.
   It needs the [.NET Framework 4.7.2 developer pack](https://dotnet.microsoft.com/download/dotnet-framework/net472) (or Visual Studio with the .NET desktop workload).

   ```bash
   dotnet build dm42dll/dm42dll.csproj -c Debug
   ```

3. Build the editor (Py42.Core is built along the way):

   ```bash
   dotnet build dm42editor/dm42editor.slnx -c Release
   ```

The PDF manuals (DM42, HP-42S) are not included: download them from the SwissMicros and HP websites,
then put them in `dm42editor/data/` (`dm42_user_manual.pdf`, `42s manuel utilisation.pdf`, `Quick Reference Guide.pdf`).

A single-file publish profile is provided: `dotnet publish -p:PublishProfile=FolderProfile`.

## Language

The interface exists in French and in English: *File → Settings → Language* (by default, the Windows language). The change applies at the next start. The translations are in `resources/lang_en.txt`, as `French TAB English`.

## License

GNU GPLv3. The `dm42dll` encoder / decoder is a C# port of the TypeScript code of J. Heilingbrunner
([vscode-hp42s-Free42](https://github.com/Heilingbrunner/vscode-hp42s)), under the MIT license (see `LICENCE.txt` in dm42dll).

HP-42S is a trademark of HP Inc.; DM42 is a SwissMicros product. This project is not affiliated with either company.
