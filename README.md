# LabelPrint 1.06

.NET 8 Windows Forms rebuild of Jumping Gym LabelPrint 1.6 (`CardPrint4.vbp`, `ExeName32=LabelPrint.exe`). The startup form is `frmMemberCard` (caption 會員卡Label列印系統 V1.6). Employee badges are the same form, under 員工 - 工作証. Captions are the Traditional Chinese strings from the `.frm` files (Big5).

Member cards are read, not written. The card id is 5 bytes at offset 248 (`IC_Read_Hex` on RD600P, APDU `FF B0 00 F8 05` on ACR38U). SQL and the card PIN come from configuration. The VB6 modules contained a staff-database login; that password is not in this repository.

The original binary is PE32 x86 (`MSVBVM60`), SHA256 `55e056f67fa784fb76e9e90b371f3712daa22c276ca8977e58b67068aa5fcd75`. No `.vbp` / `.frm` source was available, so behaviour is reconstructed from the form and control names, the template file names, and the MIS tables named in that binary. Hardware calls and the legacy `.lbl` binary format still need a pass on a desk PC. Those spots are marked `TODO(on-machine)`.

## Build and run

Requires the .NET 8 SDK. The application project targets `net8.0-windows` / `win-x86`, because `IC_D600.dll` and `acsr2032.dll` are 32-bit. The build is framework-dependent: the desk PC needs the .NET 8 **x86** desktop runtime. `dotnet build` produces `LabelPrint.exe` (a 32-bit Windows GUI host) even when the SDK itself is running on another OS.

```bash
dotnet build LabelPrint.sln
dotnet test LabelPrint.sln
dotnet run --project src/LabelPrint.App
```

`EnableWindowsTargeting` is set so the solution also builds on a non-Windows SDK. The UI and the card reader still run only on Windows.

Output layout (beside `LabelPrint.exe`, in `src/LabelPrint.App/bin/Debug/net8.0-windows/win-x86/`):

```
LabelPrint.exe
appsettings.json
CardReader.config
IC_D600.dll          (you supply this; not in git)
acsr2032.dll         (you supply this; not in git)
templates/*.lbl
```

A file dropped directly beside the exe wins over the copy in `templates\`. That is how a captured legacy template replaces the shipped text layout.

## Configuration

`src/LabelPrint.App/appsettings.json` ships with an empty connection string and an empty card PIN. Three ways to set the real values, highest priority last:

1. `appsettings.Local.json` beside the exe (gitignored).
2. User secrets on a developer machine.
3. Environment variables or command-line arguments.

User secrets:

```bash
dotnet user-secrets set "ConnectionStrings:Mis" "Server=YOUR_SQL_HOST;Database=MIS;User ID=YOUR_USER;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=True;" --project src/LabelPrint.App
dotnet user-secrets set "LabelPrint:Card:PasswordHex" "" --project src/LabelPrint.App
```

Environment variable: `ConnectionStrings__Mis`.

Copy `config/appsettings.Local.sample.json` to `appsettings.Local.json` next to the exe and replace the placeholders. The sample is:

```json
{
  "ConnectionStrings": {
    "Mis": "Server=YOUR_SQL_HOST;Database=MIS;User ID=YOUR_USER;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=True;"
  },
  "LabelPrint": {
    "Card": {
      "PasswordHex": ""
    }
  }
}
```

A string that still contains `YOUR_SQL_HOST`, `YOUR_USER`, `YOUR_PASSWORD`, or `YOUR_SECRET` is treated as not configured. The app reads MIS and does not update it.

`LabelPrint:Card:PasswordHex` is the SLE4442 (3 bytes) or SLE4428 (2 bytes) PIN as hex, for example `FFFFFF`. Leave it empty to skip the PIN call. Probe never sends a PIN. A wrong SLE4442 PIN can lock the card. Do not put the PIN in `CardReader.config`.

`LabelPrint:PrinterName` overrides the default printer. Empty uses the Windows default. `LabelPrint:TemplateDirectory` is an extra folder searched before the exe directory.

### MIS columns

Known columns are queried with parameters. Names that come from config are restricted to a single identifier (or `schema.table`) and bracketed.

| Config | Default | Legacy |
| --- | --- | --- |
| `Leager:Table` | `leager` | member table |
| `Leager:NumberColumn` | `leag_no` | `txtLeag_No` |
| `Leager:NameColumn` | `leag_name` | `txtLeag_Name` |
| `Leager:OldCardNumberColumn` | `oldcardno` | card number |
| `Leager:ValidDateColumn` | `validdate` | `txtValiddate` |
| `Leager:MemberCodeColumn` | empty | `txtMemberCode` stays manual until the column is confirmed |
| `Leager:TempMemberColumn` | empty | `txtTempMem` stays manual until the column is confirmed |
| `MtUser:Table` | `MT_USER` | employee table |
| `MtUser:StaffNumberColumn` | `MU_StaffNo` | `txtStaffNo` |
| `MtUser:EnglishNameColumn` | `MU_Name` | English name |
| `MtUser:ChineseNameColumn` | `MU_CHIName` | selected, not printed |
| `MtUser:IdColumn` | `MU_ID` | card-back number typed into 卡號碼 |
| `MtUser:EnableColumn` | `mu_enable` | must be 1 |
| `MtUser:EntryDateColumn` | `mu_entryDate` | newest row wins |
| `Leager:CardNoColumn` | `CardNo` | IC id from offset 248 |
| `Leager:NetFlagColumn` | `netflag` | count requires `'Y'` |

The label query uses `CONVERT(varchar(10), validdate, 111)` (`yyyy/mm/dd`). The printed valid date is the first four characters, except Good Luck 卡 which prints 2020年1月12日.

### Card id

`frmMemberCard` reads 5 bytes at offset 248 and treats `FFFFFFFFFF` as no card. RD600P calls `IC_InitComm(101)`, `IC_Status`, `IC_InitType(icdev, 16)`, `IC_ReadCount_SLE4442`, then `IC_Read_Hex`. It does not send a PIN. ACR38U sends APDU `FF B0 00 F8 05` to `ACS CCID USB Reader 0`.

## Native DLLs

Copy these beside `LabelPrint.exe`. They are not in this repo.

| File | Role |
| --- | --- |
| `IC_D600.dll` | RD600P declares in `IC_D600.bas` |
| `acsr2032.dll` | ACR30U (`ACSR2032.DLL` in the VB6 module). Preloaded when it sits beside the exe |

The P/Invoke layer loads both by full path from the exe directory. Calling convention is StdCall. `IC_InitComm` returns a VB6 `Long` (32-bit handle). The other listed calls return `Integer` (16-bit). A wrong DLL can still crash the process.

`CardReader.config` is the model name only. A per-user copy under `%LocalAppData%\LabelPrint\CardReader.config` wins over the file beside the exe. If the exe directory is not writable, Save falls back to that user file. The file refuses a password entry.

## Templates

Shipped files in `templates\` are text layouts (`Format=LabelPrint.TextLayout.v1`) so the desk can print before the legacy binaries are decoded:

| File | Use |
| --- | --- |
| `Empnameplate1.lbl` / `Empnameplate2.lbl` | `MemID` = staff number, `MemName` = English name. Chosen by name length |
| `nameplate1.lbl` / `nameplate2.lbl` | `MemID` = `oldcardno`, `MemName`, `ValidDate` = year. Chosen by name length |
| `nameplate3.lbl` | Good Luck 卡 |

`Source` may also be `StaffNo`, `EmpName`, `LeagNo`, `LeagName`, `CardNo`, `MemberCode`, and the other names in `NameplateFields`. The BrssCom names are `MemID`, `MemName`, and `ValidDate`.

The loader accepts a JPEG, PNG, BMP, or GIF `.lbl` and paints the built-in text on top. An OLE compound file (the real BrssCom template) is reported and the print path asks before using the built-in layout. Search order is the configured template directory, the exe directory, then `templates\`.

Paper size sent to the printer is the template size in millimetres, with zero margins. Preview is on the form so a layout can be checked without feeding a label.

## What the VB6 source actually does

`CardPrint4.vbp` starts `frmMemberCard`. The menu is a single item, 選擇讀卡器, which opens `frmReaderModel` (caption still says V1.0). `CardReader.config` is one token: `ACR30U`, `ACR38U`, or `RD600P` (the shipped file is `RD600P`).

| Mode | Behaviour |
| --- | --- |
| 一般會員 | Read the card, map `CardNo` to `leag_no`, show `oldcardno` / `leag_name` / `validdate` |
| 試玩會員 | Type number, name, and `20__-12-31`. No card read |
| 員工 - 工作証 | Numeric card-back number → `MT_USER.MU_ID` where `mu_enable = 1`. Prints `MU_StaffNo` and English `MU_Name` |
| Good Luck 卡 | Same read as 一般會員. Preview says 有效期至 / 2020年1月12日. Template is `nameplate3.lbl` |

A name shorter than 15 characters uses `nameplate1.lbl` or `Empnameplate1.lbl`. A longer name uses `nameplate2.lbl` or `Empnameplate2.lbl`. Print goes through `BrssCom.Document` and sets `MemID`, `MemName`, and (for members) `ValidDate`. Crystal Reports (`mcReportPrint`) is referenced by the project and is commented out of the print button.

If `leager` has no `netflag = 'Y'` row for that `CardNo`, the VB6 app runs `EXECUTE net_c_interface` once and shows 會員正在過場. A 5-second timer then reads the card again. This program does the same, with parameters instead of concatenated SQL.

Member SQL uses `ConnectionStrings:Shiyu` when that value is set, otherwise `ConnectionStrings:Mis`. Employee SQL uses `Mis`. Both stay empty in `appsettings.json`.

## Legacy form mapping

| VB6 | This app |
| --- | --- |
| `frmMemberCard` | `MainForm` |
| `frmReaderModel` | `ReaderModelForm` |
| `optMember(0..3)` | 一般會員, 試玩會員, 員工 - 工作証, Good Luck 卡 |
| `cmdRead` | Button caption ＩＣ咭編號： |
| `txtCardNo`, `txtMemberCode`, `txtLeag_Name`, `txtValiddate` | Labels filled from the card and `leager` |
| `txtTempMem(0..2)` | Trial member number, name, date |
| `txtCardNum`, `cmdGetEmp`, `txtStaffNo`, `txtEmpName` | Employee card-back lookup |
| `picLabel1`, `picLabel2` | Short-name and long-name preview |
| `StatusBar1` | 分店, database name, reader status, 讀卡器型號 |
| `BrssCom.Document` | Text `.lbl` stand-in, or built-in layout with the same three fields |

## Remaining gaps

- **BrssCom.Document.** The gym `.lbl` files are OLE compound documents. This program cannot call `BrssCom.Document.Open` / `SetText` / `DoPrint` until that COM server is registered on the desk PC. A real `.lbl` beside the exe is detected and the print path asks before using the built-in `MemID` / `MemName` / `ValidDate` layout. The files under `templates\` are editable text stand-ins.
- **Crystal Reports.** `craxdrt.dll` is referenced and `mcReportPrint` still exists, but the print button does not call it. Not ported.
- **ACR30U.** Needs `ACSR2032.DLL` (`AC_Open`, `AC_StartSession`, read at 248). The desk shows the VB6 failure text plus a TODO. RD600P (`IC_InitComm(101)`, `IC_InitType 16`, `IC_Read_Hex`) and ACR38U (`ACS CCID USB Reader 0`) are implemented and still need a reader.
- **Reader photos** stored in `frmReaderModel.frx` are not in the picker.
- The VB6 reader-open failure runs `End` and quits the process. This program leaves the window open.
- `IC_D600` integer widths follow the `.bas` declares. A wrong DLL build can still crash the process. Probe on a spare reader before a live card.
- Confirm `net_c_interface` on the branch database before relying on 會員正在過場. It is a stored-procedure call, not a read.

## Projects

| Project | What it owns |
| --- | --- |
| `LabelPrint.Core` | Records, template model, options, repository and reader interfaces |
| `LabelPrint.Data` | Parameterized `leager` and `MT_USER` queries, `SELECT 1` connection check |
| `LabelPrint.Printing` | `.lbl` loader and the nameplate job (template plus field values) |
| `LabelPrint.CardReader` | `CardReader.config`, PC/SC list, `IC_D600` gateway |
| `LabelPrint.App` | WinForms shell, GDI render, `PrintDocument` |
| `LabelPrint.Tests` | SQL safety, templates, config, card codec. No database and no reader hardware |

Log file: `%LocalAppData%\LabelPrint\logs\labelprint.log`.

## On-machine follow-up (still open)

Search the repo for `TODO(on-machine)`. The gaps that still need the desk PC are listed above: BrssCom, Crystal Reports (unused by the live button), ACR30U, and a live RD600P / ACR38U read. Calibrate paper size against the label stock. Rotate the staff SQL password that was in the VB6 module; it was not copied here.
