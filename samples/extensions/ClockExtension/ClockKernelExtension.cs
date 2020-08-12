// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FSharp.Compiler.Scripting;
using FSharp.Compiler.SourceCodeServices;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.CSharp;
using Microsoft.DotNet.Interactive.Formatting;
using Microsoft.DotNet.Interactive.FSharp;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using System.Linq;

using static FSharp.Compiler.Interactive.Shell;
using System.Collections.Generic;

namespace ClockExtension
{

    public class ClockKernelExtension : IKernelExtension
    {
        private ConditionalWeakTable<FSharpChecker, FSharpChecker> checkers = new ConditionalWeakTable<FSharpChecker, FSharpChecker>();

        public async Task OnLoadAsync(Kernel kernel)
        {
            System.Diagnostics.Debugger.Launch();



            //var kernels = new List<FSharpKernel>();

            //var c = kernel as CompositeKernel;
            //if(c!=null)
            //{
            //    kernels.AddRange(c.ChildKernels.Where(k => k is FSharpKernel).Select(k => (FSharpKernel)k));
            //}

            //if (kernel is FSharpKernel)
            //{
            //    kernels.Add((FSharpKernel)kernel);
            //}
            //foreach(var fs in kernels)
            //{ 

            //    var allFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            //    var scripty = (Lazy<FSharpScript>)fs.GetType().GetField("script", allFlags).GetValue(fs);
            //    var fsi = scripty.Value.Fsi;

            //    var checkerField = fsi.GetType().GetField("checker", allFlags);
            //    var old = (FSharpChecker)checkerField.GetValue(fsi);

            //    if (!checkers.TryGetValue(old, out var foo))
            //    {
            //        var resolver = (FSharp.Compiler.ReferenceResolver.Resolver)old.GetType().GetField("legacyReferenceResolver", allFlags).GetValue(old);
            //        var nc = FSharpChecker.Create(null, FSharpOption<bool>.Some(true), null, FSharpOption<FSharp.Compiler.ReferenceResolver.Resolver>.Some(resolver), null, null, null, null);
            //        checkerField.SetValue(fsi, nc);
            //        checkers.Add(nc, nc);
            //    }
            //}



            kernel.AddMiddleware((a, b,c) =>
            {
                var fs = b.HandlingKernel as FSharpKernel;
                if (fs != null)
                {
                    Func<string, System.Threading.Tasks.Task> cont = null;
                    string code = null;

                    switch(b.Command)
                    {
                        case SubmitCode v:
                            code = v.Code;
                            cont = n => c(new SubmitCode(n, v.TargetKernelName, v.SubmissionType), b);
                            break;
                        case RequestHoverText v:
                            code = v.Code;
                            cont = n => c(new RequestHoverText(n, v.LinePosition, v.TargetKernelName), b);
                            break;
                        case RequestCompletions v:
                            code = v.Code;
                            cont = n => c(new RequestCompletions(n, v.LinePosition, v.TargetKernelName), b);
                            break;
                        case RequestDiagnostics v:
                            code = v.Code;
                            cont = n => c(new RequestDiagnostics(n, v.TargetKernelName), b);
                            break;

                    }

                    if (code != null)
                    {
                        var allFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                        var scripty = (Lazy<FSharpScript>)fs.GetType().GetField("script", allFlags).GetValue(fs);
                        var trip = FSharpAsync.RunSynchronously(scripty.Value.Fsi.ParseAndCheckInteraction(code), FSharpOption<int>.None, FSharpOption<CancellationToken>.None);

                        typeof(FSharpCheckFileResults).GetField("keepAssemblyContents", allFlags).SetValue(trip.Item2, true);

                        var newCode = Adaptify.Compiler.Adaptify.getReplacementCode(Adaptify.Compiler.Log.empty, false, trip.Item2, code);

                        var rx = new System.Text.RegularExpressions.Regex(@"FSI_[0-9][0-9][0-9][0-9]\.");

                        newCode = newCode.Replace("Stdin.", "");
                        newCode = rx.Replace(newCode, "");

                        return cont(newCode);
                    }
                }
                return c(a, b);
            });
            
            Formatter<DateTime>.Register((date, writer) =>
            {
                writer.Write(date.DrawSvgClock());
            }, "text/html");

            Formatter<DateTimeOffset>.Register((date, writer) =>
            {
                writer.Write(date.DrawSvgClock());
            }, "text/html");


            var clockCommand = new Command("#!clock", "Displays a clock showing the current or specified time.")
                {
                    new Option<int>(new[]{"-o","--hour"},
                                    "The position of the hour hand"),
                    new Option<int>(new[]{"-m","--minute"},
                                    "The position of the minute hand"),
                    new Option<int>(new[]{"-s","--second"},
                                    "The position of the second hand")
                };

            clockCommand.Handler = CommandHandler.Create(
                async (int hour, int minute, int second, KernelInvocationContext context) =>
            {
                await context.DisplayAsync(SvgClock.DrawSvgClock(hour, minute, second));
            });

            kernel.AddDirective(clockCommand);

            if (KernelInvocationContext.Current is { } context)
            {
                context.Display(kernel.GetType().FullName);
                await context.DisplayAsync($"asdasdasdasdasd `{nameof(ClockExtension)}` is loaded. It adds visualizations for `System.DateTime` and `System.DateTimeOffset`. Try it by running: `display(DateTime.Now);` or `#!clock -h`", "text/markdown");
            }
        }
    }
}