using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.Scripting;

namespace EtlKit.Scripting;

/// <summary>
/// Wrapper around the script to allow calling RunAsync with ExpandoObject
/// </summary>
/// <typeparam name="TOutput">Result type</typeparam>
[PublicAPI]
public class ScriptRunner<TOutput>
{
    public Script<TOutput> Script { get; }
    public GlobalsTypeInfo GlobalsTypeInfo { get; }

    /// <summary>
    /// Default constructor
    /// </summary>
    /// <param name="script">Script source</param>
    /// <param name="globalsTypeInfo">Globals type info</param>
    public ScriptRunner(Script<TOutput> script, GlobalsTypeInfo globalsTypeInfo)
    {
        Script = script;
        GlobalsTypeInfo = globalsTypeInfo;
    }

    public async Task<ScriptState<TOutput>> RunAsync<TInput>(
        TInput globals,
        CancellationToken cancellationToken = default
    )
    {
        if (globals is not IDictionary<string, object?> expando)
        {
            return await Script.RunAsync(globals, cancellationToken).ConfigureAwait(false);
        }

        dynamic args = Activator.CreateInstance(GlobalsTypeInfo.Type, expando)!;
        return await Script.RunAsync(args, cancellationToken).ConfigureAwait(false);
    }
}
