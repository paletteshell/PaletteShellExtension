// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PaletteShellExtension.Classes;
using System;

namespace PaletteShellExtension;

public partial class PaletteShellExtensionCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;

    public PaletteShellExtensionCommandsProvider()
    {
        // This constructor runs during COM activation; an exception escaping it kills the
        // extension process on every launch. The page/settings constructors guard their own
        // failure modes, but this is the backstop: whatever happens, hand the host a valid
        // provider — worst case a single entry that says why nothing else is here.
        DisplayName = "PaletteShell";
        try
        {
            Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
            Settings = PaletteShellSettingsManager.Instance.Settings;
            _commands = [
                new CommandItem(new PaletteShellExtensionPage()) { Title = DisplayName, Subtitle = "Run your PowerShell scripts" },
            ];
        }
        catch (Exception ex)
        {
            Log.Error("PaletteShell provider failed to initialize", ex);
            _commands = [
                new CommandItem(new NoOpCommand())
                {
                    Title = DisplayName,
                    Subtitle = "PaletteShell failed to load — check logs in %LOCALAPPDATA%\\PaletteShell\\logs",
                },
            ];
        }
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

}
