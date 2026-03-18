# Sealed Journal

A secure, encrypted notepad application for Windows with daily journaling support.

## Features

- **AES-256 Encryption**: All notes are encrypted using AES-256 with PBKDF2 key derivation (100,000 iterations)
- **Password Protected**: Requires password to access and view notes
- **Daily Journal Files**: Notes are automatically organized by date (YYYY-MM-DD.txt)
- **Global Hotkey**: Quick show/hide with configurable keyboard shortcut
- **Append-Only Mode**: Press Enter to save and clear, keeping previous entries hidden
- **Date Stamping**: Optional automatic timestamp insertion

## Security

Sealed Journal uses industry-standard cryptographic practices:

- **AES-256** symmetric encryption
- **PBKDF2** with SHA-256 for key derivation
- **Random salt** (32 bytes) per encryption operation
- **Random IV** (16 bytes) per encryption operation
- **100,000 iterations** for key derivation

Each encrypted block contains: `[salt][iv][ciphertext]` encoded as Base64.


## Installation

### Prerequisites
- Windows 10/11
- .NET 9.0 Runtime

### Build from Source

```bash
git clone https://github.com/yourusername/SealedJournal.git
cd SealedJournal
dotnet build
dotnet test
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The self-contained executable will be at `bin\Release\net9.0-windows7.0\win-x64\publish\notepad.exe`.

### Run

```bash
dotnet run
```

Or build a release:

```bash
dotnet build -c Release
```

The executable will be in `bin/Release/net9.0-windows7.0/`

## Configuration

Copy `appsettings.example.json` to `appsettings.json` and customize:

```json
{
  "Hotkey": {
    "Modifiers": [ "Ctrl", "Shift" ],
    "Key": "OemMinus"
  },
  "InsertDateKey": "F7",
  "NotesDirectory": "notes",
  "Window": {
    "Width": 800,
    "Height": 600
  }
}
```

### Hotkey Configuration

- **Modifiers**: Array of `"Ctrl"`, `"Alt"`, `"Shift"`, `"Win"` (any combination)
- **Key**: The primary key (e.g., `"A"`, `"F1"`, `"OemMinus"`, `"Space"`)

Supported keys include:
- Letters: `A` through `Z`
- Numbers: `0` through `9`
- Function keys: `F1` through `F12`
- Special: `OemMinus` (-), `OemPlus` (+), `Space`, `Enter`, `Escape`, `Tab`

### Other Settings

- **InsertDateKey**: Key to insert current timestamp (default: `F7`)
- **NotesDirectory**: Folder name for storing notes (default: `notes`)
- **Window**: Initial window dimensions

## Usage

### First Run

1. Launch Sealed Journal
2. Enter a password (this will be used for all encryption)
3. Start typing notes

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Enter` | Save current text and clear (append-only mode) |
| `[InsertDateKey]` | Insert timestamp at cursor position |
| `Ctrl+E` | Toggle edit/view mode (requires password) |
| `[Configured Hotkey]` | Show/hide window globally |

### Modes

- **Append Mode** (default): Type text and press Enter to save. Previous content is hidden.
- **View Mode**: Click "View" button (requires password) to see all content.
- **Edit Mode**: In View mode, press `Ctrl+E` to enable editing.

### File Selection

Use the dropdown at the top to switch between different days' notes.

## File Storage

Notes are stored in the configured `NotesDirectory` (default: `notes/`):

```
notes/
├── 2025-03-17.txt
├── 2025-03-18.txt
└── 2025-03-19.txt
```

Each file contains one or more Base64-encoded encrypted blocks, one per line.

## Security Considerations

1. **Password**: Choose a strong password. The encryption is only as secure as your password.
2. **Memory**: The password is held in memory during the session. A memory dump could potentially expose it.
3. **File Access**: The encrypted files are accessible on disk. Ensure proper file system permissions.
4. **No Recovery**: If you forget your password, there is no way to recover your notes.

## Development

### Project Structure

```
SealedJournal/
├── SealedJournal.cs       # Main application code
├── SealedJournal.csproj   # Project configuration
├── appsettings.json       # User configuration (gitignored)
├── appsettings.example.json  # Example configuration
├── SealedJournal.Tests/   # Unit tests
└── README.md
```

### Running Tests

```bash
dotnet test
```

## License

MIT License - see LICENSE file for details.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Run tests
5. Submit a pull request

## Changelog

### v2.0.0
- Complete security overhaul with proper AES-256 encryption
- PBKDF2 key derivation with random salt
- Random IV per encryption operation
- Configurable hotkeys via appsettings.json
- Added Cancel button to password dialog
- Improved error handling (no raw exception messages)