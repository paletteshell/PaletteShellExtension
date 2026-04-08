# PaletteShell Extension

**PaletteShell** is a Windows Command Palette extension that lets you run custom PowerShell scripts directly from the Command Palette. Transform clipboard text, generate GUIDs, format JSON, and automate your daily workflows—all without leaving your keyboard.

## 🌟 Features

- **🚀 Quick Access**: Run PowerShell scripts directly from Windows Command Palette (Win + Space)
- **📋 Clipboard Utilities**: Transform and manipulate clipboard text with one keystroke
- **🔧 Customizable**: Create your own scripts with rich metadata and parameters
- **📝 Parameter Support**: Interactive forms for scripts that require user input
- **🎨 Rich Metadata**: Organize scripts with icons, groups, descriptions, and tags
- **⚡ Cross-Platform PowerShell**: Supports both PowerShell Core (pwsh) and Windows PowerShell
- **🔒 Security**: Runs in user context with optional admin elevation support

## 📦 What's Included

### Sample Scripts

PaletteShell comes with 13+ ready-to-use scripts:

#### Clipboard Transformations
- **Clipboard → CSV**: Convert lines to comma-separated, quoted CSV
- **Clipboard → UpperCase**: Convert text to UPPERCASE
- **Clipboard → LowerCase**: Convert text to lowercase
- **Trim Lines**: Remove leading/trailing whitespace
- **Sort Lines**: Alphabetically sort lines
- **Remove Duplicate Lines**: Clean up repeated entries
- **URL Encode/Decode**: Encode or decode URL parameters
- **Base64 Encode/Decode**: Encode/decode Base64 text

#### JSON Utilities
- **JSON Format**: Pretty-print minified JSON with proper indentation
- **JSON Minify**: Compress JSON by removing whitespace

#### Generators
- **Generate GUID**: Create a new UUID/GUID
- **Unix Timestamp**: Get the current Unix timestamp

All scripts are stored in `%USERPROFILE%\Documents\PaletteShellScripts\` and can be modified or extended.

## 🏗️ Project Structure

```
PaletteShellExtension/
├── Assets/                          # App icons and visual assets
├── Classes/
│   ├── ScriptItem.cs               # Script list item data model
│   ├── ScriptManifest.cs           # Script manifest with parameters
│   └── ScriptParameter.cs          # Script parameter definition
├── Commands/
│   ├── OpenFolderCommand.cs        # Command to open scripts folder
│   ├── ReloadPageCommand.cs        # Command to refresh script list
│   └── RunScriptCommand.cs         # Command to execute a PowerShell script
├── Forms/
│   ├── NewScriptWizardForm.cs      # Adaptive Card form for creating new scripts
│   └── ScriptParameterForm.cs      # Dynamic form for script parameters
├── Pages/
│   ├── NewScriptWizardPage.cs      # Page for script creation wizard
│   └── PaletteShellExtensionPage.cs # Main page listing all scripts
├── SampleScripts/                   # Built-in sample PowerShell scripts
│   ├── *.ps1                       # Various utility scripts
│   └── README.md                   # Sample scripts documentation
├── PaletteScriptAttributes.psm1    # PowerShell module with custom attributes
├── PowerShellScriptParser.cs       # Parser for script metadata and parameters
├── ScriptMetadata.cs               # Metadata model for scripts
├── PaletteShellExtension.cs        # Main extension entry point
├── PaletteShellExtensionCommandsProvider.cs # Command provider
├── Program.cs                       # COM server host
└── Package.appxmanifest            # MSIX package manifest
```

### Architecture Overview

**PaletteShell** is built as a Windows MSIX-packaged Command Palette extension using .NET 9 and Windows App SDK. Here's how it works:

1. **COM Server**: The extension runs as an out-of-process COM server (`Program.cs`) that implements the `IExtension` interface
2. **Command Provider**: `PaletteShellExtensionCommandsProvider` exposes the extension to Command Palette
3. **List Page**: `PaletteShellExtensionPage` displays all available scripts from the user's scripts folder
4. **Script Parser**: `PowerShellScriptParser` uses PowerShell AST to extract metadata, parameters, and help content
5. **Dynamic Execution**: `RunScriptCommand` executes scripts with proper context, parameters, and output handling
6. **Adaptive Forms**: Scripts with parameters generate dynamic forms using Adaptive Cards

### Key Components

#### ScriptMetadata
Defines script behavior through PowerShell attributes:
- **ScriptHost**: PowerShell host to use (`pwsh` or `powershell`)
- **ScriptGroup**: Category/group for organization
- **ScriptIcon**: Emoji or glyph for visual identification
- **ScriptTimeout**: Maximum execution time in milliseconds
- **ScriptOutput**: Output mode (None, Clipboard, Markdown, Toast)
- **ScriptOutputAction**: Post-execution action (None, OpenPath)
- **RequiresElevation**: Run as administrator
- **ScriptEnv**: Environment variables
- **ScriptMutex**: Mutual exclusion lock name
- **ScriptCwd**: Working directory

#### PowerShellScriptParser
Uses the PowerShell AST API to:
- Parse script-level attributes from the `param` block
- Extract comment-based help (`.SYNOPSIS`, `.DESCRIPTION`, etc.)
- Discover parameter definitions with types and validation
- Build a rich `ScriptManifest` for parameter forms

#### PaletteScriptAttributes Module
`PaletteScriptAttributes.psm1` provides:
- Custom attribute classes for script metadata
- `Get-ClipboardText` and `Set-ClipboardText` functions for cross-platform clipboard access
- Fallback to Windows Forms clipboard when TextCopy is unavailable

## 🚀 Getting Started

### Prerequisites

- **Windows 11** (version 21H2 or later) or **Windows 10** (version 20H1 or later)
- **Visual Studio 2022** (17.12 or later) with:
  - .NET desktop development workload
  - Windows application development workload
- **.NET 9 SDK**
- **PowerShell 7+** (pwsh) recommended (some scripts may work with Windows PowerShell 5.1)
- **Windows Command Palette** installed on your system

### Building the Project

1. **Clone the repository**:
   ```bash
   git clone <repository-url>
   cd PaletteShellExtension
   ```

2. **Open in Visual Studio**:
   ```bash
   start PaletteShellExtension\PaletteShellExtension.sln
   ```

3. **Restore NuGet packages**:
   - Visual Studio will automatically restore packages on first load
   - Or manually: `dotnet restore PaletteShellExtension\PaletteShellExtension.csproj`

4. **Build the solution**:
   - Press `F6` or `Ctrl+Shift+B`
   - Or: `dotnet build PaletteShellExtension\PaletteShellExtension.csproj`

### Debugging

1. **Set the startup project**: Right-click `PaletteShellExtension` → Set as Startup Project
2. **Choose a launch profile** in Visual Studio:
   - **PaletteShellExtension (Package)**: Run as packaged MSIX app (recommended for full testing)
   - **PaletteShellExtension (Unpackaged)**: Run unpackaged for faster iteration
3. **Press F5** to start debugging
4. The extension will register with Command Palette
5. Open Command Palette (Win + Space) and type "PaletteShell"

### Deployment

#### Local Deployment (Sideloading)

1. **Build in Release mode**:
   ```bash
   dotnet build PaletteShellExtension\PaletteShellExtension.csproj -c Release
   ```

2. **Create MSIX package**:
   - Right-click project → **Package and Publish** → **Create App Packages**
   - Select "Sideloading" and follow the wizard
   - Or use command line: 
     ```bash
     msbuild PaletteShellExtension\PaletteShellExtension.csproj /p:Configuration=Release /p:Platform=x64 /p:UapAppxPackageBuildMode=SideloadOnly
     ```

3. **Install the package**:
   - Double-click the generated `.msix` file
   - Or use PowerShell:
     ```powershell
     Add-AppxPackage -Path "path\to\PaletteShellExtension.msix"
     ```

4. **Verify installation**:
   - Open Command Palette (Win + Space)
   - Type "PaletteShell" to see your extension

#### Uninstalling

```powershell
Get-AppxPackage -Name "PaletteShellExtension" | Remove-AppxPackage
```

## 📝 Creating Custom Scripts

### Basic Script Template

Create a new `.ps1` file in `%USERPROFILE%\Documents\PaletteShellScripts\`:

```powershell
using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    My Custom Script
.DESCRIPTION
    A longer description of what this script does
#>
[ScriptHost('pwsh')]
[ScriptGroup('My Scripts')]
[ScriptIcon('🎯')]
[ScriptTimeout(10000)]
[ScriptOutput('None')]
param()

# Your PowerShell code here
$text = Get-ClipboardText
Write-Host "Processing: $text"
Set-ClipboardText $text.ToUpper()
```

### Using the Script Wizard

1. Open Command Palette (Win + Space)
2. Type "PaletteShell" and press Enter
3. Select **"Create new script..."**
4. Choose a template:
   - **Blank script**: Empty template
   - **Clipboard → CSV**: Transform clipboard data
   - **Index lines in file**: File processing example
5. Enter a script name and click **Create**
6. The script opens in your default editor

### Script with Parameters

Scripts can accept user input through parameters:

```powershell
using module .\PaletteScriptAttributes.psm1

<#
.SYNOPSIS
    Repeat Text
.DESCRIPTION
    Repeat clipboard text N times
.PARAMETER Count
    Number of times to repeat the text
#>
[ScriptHost('pwsh')]
[ScriptGroup('Text Utilities')]
[ScriptIcon('🔁')]
[ScriptTimeout(5000)]
param(
    [Parameter(Mandatory=$true)]
    [int]
    [ValidateRange(1, 100)]
    $Count = 3
)

$text = Get-ClipboardText
$repeated = ($text * $Count)
Set-ClipboardText $repeated
Write-Host "Repeated text $Count times"
```

When you run this script, PaletteShell will display an Adaptive Card form asking for the `Count` parameter.

### Available Attributes

| Attribute | Description | Example |
|-----------|-------------|---------|
| `[ScriptHost('pwsh')]` | PowerShell host (`pwsh` or `powershell`) | `[ScriptHost('pwsh')]` |
| `[ScriptGroup('Group')]` | Category for organization | `[ScriptGroup('Clipboard')]` |
| `[ScriptIcon('🎨')]` | Emoji or glyph icon | `[ScriptIcon('📋')]` |
| `[ScriptTimeout(5000)]` | Timeout in milliseconds | `[ScriptTimeout(10000)]` |
| `[ScriptOutput('Mode')]` | Output mode: `None`, `Clipboard`, `Markdown`, `Toast` | `[ScriptOutput('Clipboard')]` |
| `[ScriptOutputAction('Action')]` | Action after output: `None`, `OpenPath` | `[ScriptOutputAction('OpenPath')]` |
| `[ScriptCwd('C:\Path')]` | Working directory (supports tokens like `%USERPROFILE%`) | `[ScriptCwd('%USERPROFILE%')]` |
| `[ScriptEnv('VAR', 'value')]` | Environment variable | `[ScriptEnv('API_KEY', 'secret')]` |
| `[ScriptMutex('name')]` | Mutual exclusion lock | `[ScriptMutex('clipboard-lock')]` |
| `[RequiresElevation()]` | Require admin rights | `[RequiresElevation()]` |

You can also use the built-in `#Requires -RunAsAdministrator` instead of `[RequiresElevation()]`.

### Helper Functions

The `PaletteScriptAttributes.psm1` module provides clipboard utilities:

```powershell
# Get clipboard text (cross-platform)
$text = Get-ClipboardText

# Set clipboard text (cross-platform)
Set-ClipboardText "Hello, World!"
```

These functions use the TextCopy library with Windows Forms fallback for maximum compatibility.

### Parameter Types

PaletteShell supports rich parameter types through PowerShell's type system:

```powershell
param(
    [string]$Text = "Default",           # Text input
    [int]$Number = 42,                   # Number input
    [bool]$Flag = $true,                 # Boolean toggle
    [ValidateSet('A', 'B', 'C')]         # Dropdown choice
    [string]$Choice = 'A',
    [ValidateRange(1, 100)]              # Number with validation
    [int]$RangedValue = 50,
    [Parameter(Mandatory=$true)]         # Required parameter
    [string]$Required
)
```

The parser automatically generates appropriate form controls based on parameter types and validation attributes.

## 🔧 Configuration

### Script Location

By default, scripts are stored in:
```
%USERPROFILE%\Documents\PaletteShellScripts\
```

This folder is automatically created when the extension first runs. Sample scripts are copied here on first launch (without overwriting existing files).

### Reloading Scripts

After creating or modifying scripts:
1. Open PaletteShell in Command Palette
2. Select **"Reload scripts"**

Or restart Command Palette.

### Accessing the Scripts Folder

From PaletteShell menu:
1. Open Command Palette (Win + Space)
2. Type "PaletteShell" and press Enter
3. Select **"Open scripts folder"**

This opens the folder in File Explorer where you can edit scripts directly.

## 🛠️ Development Guide

### Technology Stack

- **.NET 9** with Windows App SDK
- **Microsoft.CommandPalette.Extensions** - Command Palette extension APIs
- **System.Management.Automation** - PowerShell AST parsing and metadata extraction
- **TextCopy** - Cross-platform clipboard access
- **Shmuelie.WinRTServer** - Out-of-process COM server hosting
- **MSIX Packaging** - Modern Windows app deployment

### Build Configuration

The project supports:
- **Debug builds**: Trimming disabled, AOT analyzers enabled for fast iteration
- **Release builds**: Full trimming enabled for smaller deployment size
- **AOT-ready**: Configured for Native AOT compilation with CsWinRT optimizer
- **Platforms**: x64 and ARM64
- **Target Framework**: .NET 9 (net9.0-windows10.0.26100.0)
- **Minimum Windows Version**: Windows 10 version 20H1 (10.0.19041.0)

### Key Design Patterns

1. **AST-Based Parsing**: Uses PowerShell's own AST parser to extract metadata, parameters, and help content without executing scripts
2. **Adaptive Cards**: Script parameter forms are dynamically generated using Adaptive Cards based on parameter definitions
3. **Lazy Script Discovery**: Scripts are discovered on-demand from the file system
4. **Metadata Caching**: Script metadata is cached per session to improve performance
5. **Process Isolation**: Each script runs in its own PowerShell process with configurable timeout
6. **Single-Instance COM**: One extension instance serves all Command Palette requests

### Project Files Explained

#### Core Extension Files
- **Program.cs**: Entry point that sets up the out-of-process COM server
- **PaletteShellExtension.cs**: Main extension class implementing `IExtension`
- **PaletteShellExtensionCommandsProvider.cs**: Registers the extension with Command Palette

#### Page Management
- **PaletteShellExtensionPage.cs**: 
  - Main list page showing all scripts
  - Copies sample scripts on first run
  - Provides "Reload", "Open Folder", and "Create New" commands
  - Caches script list for performance

#### Script Execution
- **RunScriptCommand.cs**: 
  - Handles script execution with proper context
  - Manages timeouts, elevation, output capture
  - Supports mutex locks for exclusive execution
  - Processes script output based on metadata

#### Metadata System
- **PowerShellScriptParser.cs**: 
  - Parses PowerShell AST to extract attributes
  - Reads comment-based help
  - Discovers parameters with types and validation
  - Builds `ScriptManifest` for parameter forms

- **ScriptMetadata.cs**: Model for script-level metadata (host, timeout, icon, etc.)
- **ScriptManifest.cs**: Model for script with parameters
- **ScriptParameter.cs**: Model for individual parameters

#### Forms
- **NewScriptWizardForm.cs**: Adaptive Card form for creating new scripts from templates
- **ScriptParameterForm.cs**: Dynamically generates forms based on script parameters

#### PowerShell Integration
- **PaletteScriptAttributes.psm1**: 
  - PowerShell module with custom attribute classes
  - Clipboard helper functions
  - Imported by scripts using `using module` directive

### Extending the Extension

#### Adding New Script Templates

Edit `NewScriptWizardForm.cs` and add templates to the `DataJson`:

```csharp
DataJson = $$"""
{
  "templates": [
    { "title": "My Template", "value": "my-template" },
    ...
  ]
}
""";
```

Then handle the template in the `CreateScript` method:

```csharp
var scriptContent = templateKey switch
{
    "my-template" => GenerateMyTemplate(name),
    _ => GenerateBlankTemplate(name)
};
```

#### Adding New Metadata Attributes

1. **Add attribute class** in `PaletteScriptAttributes.psm1`:
   ```powershell
   class ScriptAuthorAttribute : Attribute {
       [string]$Author
       ScriptAuthorAttribute([string]$author) { $this.Author = $author }
   }
   ```

2. **Update ScriptMetadata.cs**:
   ```csharp
   public string? Author { get; set; }
   ```

3. **Parse in PowerShellScriptParser.cs**:
   ```csharp
   case "ScriptAuthorAttribute":
       metadata.Author = GetAttributeArg<string>(attr, 0);
       return true;
   ```

#### Custom Output Modes

Modify `RunScriptCommand.Invoke()` to handle new output modes:

```csharp
var output = _meta.Output?.ToLowerInvariant();
result = output switch
{
    "clipboard" => HandleClipboardOutput(stdout),
    "markdown" => HandleMarkdownOutput(stdout),
    "toast" => HandleToastOutput(stdout),
    "mymode" => HandleMyMode(stdout),  // Add custom handler
    _ => CommandResult.ShowToast("Script completed")
};
```

## 🐛 Debugging Tips

### Enable Debug Output

The extension writes detailed debug traces using `Debug.WriteLine()`. View them in Visual Studio:
- **Output** window → **Debug** pane (select "Debug" from dropdown)

Key debug prefixes:
- `[PAGE]` - Page lifecycle and script discovery
- `[RunScript]` - Script execution flow
- `[PARSER]` - AST parsing and metadata extraction

### Common Issues

**Scripts don't appear in Command Palette:**
- Check that scripts are in `%USERPROFILE%\Documents\PaletteShellScripts\`
- Files must have `.ps1` extension
- Select "Reload scripts" from PaletteShell menu
- Check Debug output for parsing errors

**Script execution fails:**
- Check PowerShell execution policy: `Get-ExecutionPolicy`
- Ensure PowerShell 7+ (pwsh) is installed if using `[ScriptHost('pwsh')]`
- Review script syntax and parameters
- Check timeout setting - increase if script takes longer

**Clipboard operations fail:**
- Ensure script uses `Get-ClipboardText` / `Set-ClipboardText` from the module
- Verify PowerShell is running in STA mode (handled automatically by the extension)
- Check that `TextCopy.dll` is in the scripts folder

**Parameters form doesn't show:**
- Verify the `param()` block is properly defined
- Check that parameters have PowerShell type annotations
- Review Debug output for AST parsing errors

**Extension doesn't load in Command Palette:**
- Verify MSIX package is installed: `Get-AppxPackage -Name "PaletteShellExtension"`
- Check that Command Palette recognizes the extension
- Look for COM registration errors in Event Viewer

## 📄 Script Metadata System

PaletteShell uses two sources for script metadata:

### 1. PowerShell Comment-Based Help
Standard PowerShell help comments provide title and description:

```powershell
<#
.SYNOPSIS
    Short one-line description (used as title)
.DESCRIPTION
    Detailed multi-line description (used as subtitle)
.PARAMETER ParamName
    Parameter description (shown in form)
#>
```

### 2. Custom Attributes
PaletteShell-specific attributes control behavior and presentation:

```powershell
using module .\PaletteScriptAttributes.psm1

[ScriptHost('pwsh')]           # Which PowerShell to use
[ScriptGroup('Utilities')]     # Grouping/category
[ScriptIcon('🔧')]            # Display icon (emoji or glyph)
[ScriptTimeout(10000)]         # 10 second timeout
[ScriptOutput('Clipboard')]    # Where to send output
param()
```

The `PowerShellScriptParser` class uses PowerShell's AST API to extract both types of metadata without executing the script.

### Execution Flow

1. User selects script from Command Palette
2. Parser checks for parameters in `param()` block
3. **If parameters exist**: Show Adaptive Card form → Collect input → Execute
4. **If no parameters**: Execute immediately
5. Script runs in isolated PowerShell process
6. Output captured and processed according to `ScriptOutput` setting
7. Timeout enforced if `ScriptTimeout` is specified
8. Result shown to user (toast, markdown viewer, etc.)

## 🔐 Security Considerations

- **Execution Policy**: Scripts run with `-ExecutionPolicy Bypass` to allow local scripts
- **User Context**: Scripts run in your user context by default
- **Admin Elevation**: Use `[RequiresElevation()]` or `#Requires -RunAsAdministrator` for admin scripts
- **Process Isolation**: Each script runs in a separate process with configurable timeout
- **No Auto-Update**: Sample scripts are copied once; your modifications are preserved
- **No Remote Execution**: Scripts are only loaded from the local filesystem
- **STA Mode**: Scripts run in Single-Threaded Apartment mode for clipboard safety

## 📜 License

This project is licensed under the MIT License. See the LICENSE file in the project root for details.

## 🤝 Contributing

Contributions are welcome! Here's how you can help:

1. **Report bugs**: Open an issue with reproduction steps and debug output
2. **Suggest features**: Describe your use case and proposed solution
3. **Submit scripts**: Share useful scripts via pull request to SampleScripts/
4. **Improve docs**: Help make the documentation clearer

### Coding Standards

- Follow existing code style and conventions
- Use C# 12 features where appropriate (file-scoped namespaces, primary constructors, etc.)
- Add XML doc comments for public APIs
- Test scripts with both PowerShell 7+ and Windows PowerShell 5.1 when possible
- Use `Debug.WriteLine()` for diagnostic logging
- Handle edge cases (missing files, empty clipboard, invalid JSON, etc.)

### Pull Request Process

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Make your changes with clear commit messages
4. Build and test locally
5. Submit a pull request with a clear description

## 🔗 Related Projects

- [Microsoft Command Palette](https://aka.ms/commandpalette) - The host application for extensions
- [PowerShell](https://github.com/PowerShell/PowerShell) - The scripting engine
- [TextCopy](https://github.com/CopyText/TextCopy) - Cross-platform clipboard library
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK) - Windows development framework

## 📞 Support

For issues, questions, or suggestions:
- **Issues**: Open a GitHub issue with debug output
- **Discussions**: Start a GitHub discussion for questions
- **Documentation**: Check the [SampleScripts README](PaletteShellExtension/SampleScripts/README.md) for script examples

## 💡 Usage Tips

- **Fast Script Editing**: Select "Open scripts folder" to quickly edit scripts
- **Grouping**: Use `[ScriptGroup()]` to organize related scripts together
- **Icon Consistency**: Use consistent emoji prefixes for related scripts (📋 for clipboard, 🔧 for utilities)
- **Error Handling**: Use `try/catch` in scripts and provide meaningful error messages with `Write-Host`
- **Testing**: Test scripts standalone in PowerShell before adding to PaletteShell
- **Timeouts**: Set realistic timeouts - scripts that hang block Command Palette
- **Output**: Use `Write-Host` (or `6>&1` redirection) for output that should be captured

## 🎯 Roadmap

Potential future enhancements:
- Script marketplace/sharing
- Script version control integration
- Advanced parameter types (file pickers, color pickers, date pickers)
- Script scheduling and automation
- Multi-step script wizards with progress
- Script templates from community
- Hot-reload for script changes
- Script debugging integration
- Custom script groups and favorites
- Script search and filtering

## 🙏 Acknowledgments

Built with:
- **Microsoft Command Palette SDK** - Extension framework
- **.NET 9** - Modern .NET runtime
- **PowerShell AST** - Script parsing
- **Adaptive Cards** - Dynamic forms

---

**Made with ❤️ using .NET 9 and Windows App SDK**

*Press Win + Space, type "PaletteShell", and start automating!*