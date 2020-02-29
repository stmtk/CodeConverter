﻿using System.Collections.Generic;
using System.Threading.Tasks;
using ICSharpCode.CodeConverter.CSharp;
using ICSharpCode.CodeConverter.Shared;
using ICSharpCode.CodeConverter.Util;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.VisualBasic;

namespace ICSharpCode.CodeConverter.VB
{
    /// <remarks>
    /// Can be stateful, need a new one for each project
    /// </remarks>
    internal class CSToVBProjectContentsConverter : IProjectContentsConverter
    {
        private readonly VisualBasicCompilationOptions _vbCompilationOptions;
        private readonly VisualBasicParseOptions _vbParseOptions;
        private Project _sourceCsProject;
        private Project _convertedVbProject;
        private VisualBasicCompilation _vbViewOfCsSymbols;
        private Project _vbReferenceProject;

        public CSToVBProjectContentsConverter(ConversionOptions conversionOptions)
        {
            var vbCompilationOptions =
                (VisualBasicCompilationOptions)conversionOptions.TargetCompilationOptionsOverride ??
                VisualBasicCompiler.CreateCompilationOptions(conversionOptions.RootNamespaceOverride);

            if (conversionOptions.RootNamespaceOverride != null) {
                vbCompilationOptions = vbCompilationOptions.WithRootNamespace(conversionOptions.RootNamespaceOverride);
            }

            _vbCompilationOptions = vbCompilationOptions;
            _vbParseOptions = VisualBasicParseOptions.Default;
            RootNamespace = conversionOptions.RootNamespaceOverride;
        }

        public string RootNamespace { get; }
        public Project Project { get; private set; }

        public string LanguageVersion { get { return _vbParseOptions.LanguageVersion.ToDisplayString(); } }


        public async Task InitializeSourceAsync(Project project)
        {
            // TODO: Don't throw away solution-wide effects - write them to referencing files, and use in conversion of any other projects being converted at the same time.
            project = await CaseConflictResolver.RenameClashingSymbols(project);
            _sourceCsProject = project;
            _convertedVbProject = project.ToProjectFromAnyOptions(_vbCompilationOptions, _vbParseOptions);
            _vbReferenceProject = project.CreateReferenceOnlyProjectFromAnyOptions(_vbCompilationOptions);
            _vbViewOfCsSymbols = (VisualBasicCompilation)await _vbReferenceProject.GetCompilationAsync();
            Project = project;
        }

        public async Task<SyntaxNode> SingleFirstPass(Document document)
        {
            return await CSharpConverter.ConvertCompilationTree(document, _vbViewOfCsSymbols, _vbReferenceProject);
        }

        public async Task<(Project project, List<WipFileConversion<DocumentId>> firstPassDocIds)>
            GetConvertedProject(WipFileConversion<SyntaxNode>[] firstPassResults)
        {
            return _convertedVbProject.WithDocuments(firstPassResults);
        }
    }
}