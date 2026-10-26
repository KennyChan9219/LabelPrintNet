# LabelPrint 1.06

.NET 8 Windows Forms rebuild of Jumping Gym's VB6 LabelPrint 1.06 desk application. It prints employee and member nameplates, reads and writes SLE4442/SLE4428 member cards through `IC_D600.dll`, and looks up MIS SQL Server. The connection string and card PIN come from configuration. They are not in source.

The desk UI (forms, menus, buttons, status text, and message boxes) is Traditional Chinese (Hong Kong, zh-Hant-HK). Control names in code stay English. The only caption recovered from the VB6 ASCII strings was `Member Name` (`lblMemName` / `lblMemName2`), shown as 會員姓名. Other captions use Hong Kong wording for the same controls (列印, 儲存, 設定, 檔案, 結束).

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
| `MtUser:ChineseNameColumn` | `MU_CHIName` | printed name when present |
| `MtUser:IdColumn` | `MU_ID` | loaded for templates, not shown on the form |

Dates from SQL `datetime` columns print as `yyyy-MM-dd`. The operator can edit the text before printing.

### Card memory

`LabelPrint:Card:MemberNumberOffset` (default 32) and `MemberNumberLength` (default 16) are ASCII, NUL-padded. **These offsets are not from a captured Jumping Gym card.** Dump a spare card with Member card → Dump main memory and correct the offset before writing.

`LabelPrint:Card:Exports` lists the assumed `IC_D600.dll` export names. Replace any name that `dumpbin /exports IC_D600.dll` does not show.

`InitReturnsHandle` (default true) treats a negative `IC_InitComm` result as failure and any other result as the handle. `CheckCardPresentCode` and `IoSuccessCode` default to 0.

## Native DLLs

Copy these beside `LabelPrint.exe`. They are not in this repo.

| File | Role |
| --- | --- |
| `IC_D600.dll` | SLE4428/4442 calls used by the VB6 app |
| `acsr2032.dll` | ACS 32-bit helper. Preloaded when it sits beside the exe |

The P/Invoke layer loads both by full path from the exe directory (not the process current directory). Assumed calling convention is StdCall. Assumed widths match VB6: `Integer` is 16-bit (port, offset, length, type) and `Long` is 32-bit (the reader handle). A wrong signature can crash the process. Use Settings → Reader model → Probe on a spare reader before Read, and Read before Write.

PC/SC (`winscard`) fills the reader-model list with live names such as `ACS CCID USB Reader 0`. The known ACS CCID models are also listed. Port `100` is the usual USB port number for this kind of DLL and is unconfirmed here. `CardReader.config` stores model, port, card type, and an optional `InitType`. A per-user copy under `%LocalAppData%\LabelPrint\CardReader.config` wins over the file beside the exe. If the exe directory is not writable, Save falls back to that user file.

## Templates

Shipped files in `templates\` are text layouts (`Format=LabelPrint.TextLayout.v1`) so the desk can print before the legacy binaries are decoded:

| File | Use |
| --- | --- |
| `Empnameplate1.lbl` | Employee name and staff number |
| `Empnameplate2.lbl` | Employee name, English name, staff number |
| `nameplate1.lbl` | Member name and number |
| `nameplate2.lbl` | Member name, number, valid date |
| `nameplate3.lbl` | Member name, number, card, member code, valid date |

Field `Source` is one of: `StaffNo`, `EmpName`, `EmpEnglishName`, `EmpChineseName`, `EmpId`, `LeagNo`, `LeagName`, `OldCardNo`, `CardNo`, `MemberCode`, `ValidDate`, `TempMember`. Positions are millimetres. `Background=file.jpg` is drawn under the text; a relative path is next to the `.lbl`.

The loader also accepts a `.lbl` that is really a JPEG, PNG, BMP, or GIF, and paints the built-in text positions on top. Anything else is reported as an unrecognized legacy binary and the print path asks before using the built-in layout. Search order is the configured template directory, the exe directory, then `templates\`.

Paper size sent to the printer is the template size in millimetres, with zero margins. Preview is on the form so a layout can be checked without feeding a label.

## Legacy form mapping

| VB6 | This app |
| --- | --- |
| `Form1` | `MainForm` — employee nameplate, member nameplate, status bar |
| `frmMemberCard` + `modMemberCard` | `MemberCardForm`, `CardReaderService`, `CardCodec` |
| `frmReaderModel` | `ReaderModelForm` |
| `mnuReaderModel`, `cboReaderModel` | 設定 → 讀卡機型號, model combo |
| `txtStaffNo`, `txtEmpName`, `cmdGetEmp`, `cmdPrintEmp` | Employee group |
| `txtLeag_No`, `txtLeag_Name`, `txtCardNo`, `txtMemberCode`, `txtValiddate`, `txtTempMem` | Member group and Member card |
| `optMember` | `nameplate1.lbl` / `nameplate2.lbl` / `nameplate3.lbl` |
| `picLabel1`, `picLabel2`, `lblMemName`, `lblMemName2` | The two member previews |
| `cmdPrint`, `cmdRead` | 列印會員, 讀卡 |
| `MSComctlLib.StatusBar` | Status strip: message, MIS, reader, templates |
| `CardReader.config` | Same file name |
| `Empnameplate1.lbl`, `Empnameplate2.lbl`, `nameplate1.lbl`, `nameplate2.lbl`, `nameplate3.lbl` | Same file names |

`Label1`–`Label5` in the VB6 form were unnamed captions. This form shows 員工編號, 員工姓名, 會員編號, 會員姓名, and the other field captions in Traditional Chinese. The string list did not include a member lookup button; **查詢** next to the member number is that action (`cmdGetMem`). `txtCardNo` / `txtCardNum` are one card-number box. `MU_ID` is available to a template as `EmpId` and is not shown, because the old form had no such box.

Enter in the staff-number or member-number box runs 查詢. Lookup by number is exact. If the number box is empty, 查詢 searches by name (employees: English or Chinese) or, for members, by card number when that box is the one filled in.

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

## On-machine follow-up

Search the repo for `TODO(on-machine)`.

1. `dumpbin /exports IC_D600.dll` and correct `LabelPrint:Card:Exports`. Confirm StdCall and the 16-bit versus 32-bit integer widths in `IcD600Gateway`. A mismatch can take the process down on the first native call.
2. Probe the ACS reader, then read a spare card. Map vendor return codes (`IoSuccessCode`, `CheckCardPresentCode`, `InitReturnsHandle`, port number, `InitType`).
3. Dump main memory and set `MemberNumberOffset` / `MemberNumberLength` from a real card before Write. Confirm whether the PIN is required to read.
4. Replace the text `.lbl` files with the gym's `Empnameplate*.lbl` and `nameplate*.lbl` if those are still the binary/artwork the VB6 app printed. Decode that format, or point `Background` at artwork exported from the old exe (the binary contained Photoshop JPEGs on the picture boxes).
5. Calibrate millimetre positions and the printer's paper size against the label stock. Many label drivers ignore a custom paper size.
6. Confirm `leager` column names for member code and temp member, then set `MemberCodeColumn` and `TempMemberColumn`.
7. Rotate the SQL password that was embedded in the VB6 exe. This application does not contain it.
