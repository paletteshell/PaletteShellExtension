using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PaletteShellExtension.Classes;

/// <summary>
/// The single entry point for running a script. Resolves a <see cref="ScriptExecutionPlan"/> from
/// a manifest (host, cwd, env, timeout, elevation) and runs it through <see cref="ScriptRunner"/>,
/// so every surface — the run command, the parameter form, and the List/Markdown/Result pages —
/// shares one set of execution decisions instead of each re-deriving them.
/// </summary>
internal static class ScriptExecutionService
{
    /// <summary>Resolves all execution decisions for <paramref name="manifest"/> once. This is the
    /// only place host/cwd/env/timeout/elevation are decided.</summary>
    public static ScriptExecutionPlan CreatePlan(ScriptManifest manifest, string scriptPath)
    {
        // Expand path tokens ({ScriptDir}/{Home}/{Temp}) in env values here so every route gets
        // the same expanded environment — previously only the no-parameter command did this.
        var expandedEnv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in manifest.Env)
        {
            expandedEnv[kv.Key] = PowerShellScriptParser.ExpandPathTokens(kv.Value, scriptPath) ?? "";
        }

        var declaredTimeout = manifest.TimeoutMs is > 0 ? manifest.TimeoutMs : null;

        return new ScriptExecutionPlan
        {
            ScriptPath = scriptPath,
            Host = manifest.Host ?? PaletteShellSettingsManager.Instance.DefaultHost,
            Cwd = PowerShellScriptParser.ResolveCwd(manifest.Cwd, scriptPath),
            Env = expandedEnv,
            RequiredModules = manifest.RequiredModules,
            OutputMode = manifest.Output,
            FileExtension = manifest.FileExtension,
            RequiresAdmin = ScriptElevation.RequiresElevation(manifest),
            DeclaredTimeoutMs = declaredTimeout,
            EffectiveTimeoutMs = declaredTimeout ?? PaletteShellSettingsManager.Instance.DefaultTimeoutMs,
        };
    }

    /// <summary>Runs the plan and awaits the result — for callers already off the UI thread
    /// (the List/Markdown/Result pages). Bounded by <see cref="ScriptExecutionPlan.EffectiveTimeoutMs"/>.</summary>
    public static Task<ScriptRunner.ScriptResult?> RunAsync(
        ScriptExecutionPlan plan,
        string args = "",
        bool reportProgress = true,
        CancellationToken cancellationToken = default)
        => ScriptRunner.RunScriptAndWaitAsync(
            scriptPath: plan.ScriptPath,
            args: args,
            host: plan.Host,
            cwd: plan.Cwd,
            env: plan.Env,
            requiresAdmin: plan.RequiresAdmin,
            timeoutMs: plan.EffectiveTimeoutMs,
            reportProgress: reportProgress,
            requiredModules: plan.RequiredModules,
            cancellationToken: cancellationToken);

    /// <summary>Runs the plan and blocks for the result — for the synchronous <c>Invoke()</c> and
    /// form-submit entry points.</summary>
    public static ScriptRunner.ScriptResult? RunAndWait(
        ScriptExecutionPlan plan,
        string args = "",
        bool reportProgress = true)
        => ScriptRunner.RunScriptAndWait(
            scriptPath: plan.ScriptPath,
            args: args,
            host: plan.Host,
            cwd: plan.Cwd,
            env: plan.Env,
            requiresAdmin: plan.RequiresAdmin,
            timeoutMs: plan.EffectiveTimeoutMs,
            reportProgress: reportProgress,
            requiredModules: plan.RequiredModules);

    /// <summary>Launches the plan without waiting — used when there's nothing to surface (output
    /// None with no declared timeout) or when elevation makes output capture impossible.</summary>
    public static bool RunFireAndForget(ScriptExecutionPlan plan, string args = "")
        => ScriptRunner.RunScript(
            scriptPath: plan.ScriptPath,
            args: args,
            host: plan.Host,
            cwd: plan.Cwd,
            env: plan.Env,
            requiresAdmin: plan.RequiresAdmin,
            requiredModules: plan.RequiredModules);
}
