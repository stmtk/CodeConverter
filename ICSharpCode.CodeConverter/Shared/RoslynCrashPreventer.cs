﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace ICSharpCode.CodeConverter.Shared
{
    internal static class RoslynCrashPreventer
    {
        public static IDisposable Create(Action<Exception> logError)
        {
            var FirstHandlerContainingType = (typeof(Compilation).GetTypeInfo().Assembly, "Microsoft.CodeAnalysis.FatalError");
            var SecondHandlerContainingType = (typeof(WorkspaceDiagnostic).GetTypeInfo().Assembly, "Microsoft.CodeAnalysis.ErrorReporting.FatalError");

            var codeAnalysisErrorHandler = ExchangeFatalErrorHandler(logError, FirstHandlerContainingType);
            var codeAnalysisErrorReportingErrorHandler = ExchangeFatalErrorHandler(logError, SecondHandlerContainingType);
            return new ActionDisposable(() => {
                ExchangeFatalErrorHandler(codeAnalysisErrorHandler, FirstHandlerContainingType);
                ExchangeFatalErrorHandler(codeAnalysisErrorReportingErrorHandler, SecondHandlerContainingType);
            });

        }

        /// <summary>
        /// Use this to stop the library exiting the process without telling us.
        /// https://github.com/dotnet/roslyn/issues/41724
        /// </summary>
        /// <remarks>
        /// The simplification code in particular is quite buggy, scattered with "throw ExceptionUtilities.Unreachable" with no particular reasoning for why the code wouldn't be reachable.
        /// It then uses FatalError.ReportUnlessCanceled rather than FatalError.ReportWithoutCrashUnlessCanceled causing fatal crashes with Environment.FailFast
        /// While this presumably allows them to get good low-level debugging info from the windows error reports caused, it just means that people come to this project complaining about VS crashes.
        /// See https://github.com/icsharpcode/CodeConverter/issues/521 and https://github.com/icsharpcode/CodeConverter/issues/484
        /// There are other ways to find these bugs - just run the expander/reducer on a couple of whole open source projects and the bugs will pile up.
        /// </remarks>
        private static Action<Exception> ExchangeFatalErrorHandler(Action<Exception> errorHandler, (Assembly assembly, string containingType) container)
        {
            if (errorHandler == null) return null;
            try {
                var fataErrorType = container.assembly.GetType(container.containingType);
                var fatalHandlerField = fataErrorType.GetField("s_fatalHandler");
                var originalHandler = (Action<Exception>)fatalHandlerField.GetValue(null);
                if (originalHandler != null) {
                    fatalHandlerField.SetValue(null, errorHandler);
                }
                return originalHandler;
            } catch (Exception) {
                return null;
            }
        }

        private sealed class ActionDisposable : IDisposable
        {
            private readonly Action _onDispose;

            public ActionDisposable(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public void Dispose() => _onDispose();
        }
    }
}