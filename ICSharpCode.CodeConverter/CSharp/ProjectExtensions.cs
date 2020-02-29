﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.CodeConverter.Shared;
using Microsoft.CodeAnalysis;

namespace ICSharpCode.CodeConverter.CSharp
{
    internal static class ProjectExtensions
    {
        private static char[] DirSeparators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        public static Project CreateReferenceOnlyProjectFromAnyOptions(this Project project, CompilationOptions baseOptions)
        {
            var options = baseOptions.WithMetadataImportOptionsAll();
            var viewerId = ProjectId.CreateNewId();
            var projectReferences = project.ProjectReferences.Concat(new[] {new ProjectReference(project.Id)});
            var viewerProjectInfo = project.ToProjectInfo(viewerId, project.Name + viewerId, options,
                projectReferences);
            var csharpViewOfVbProject = project.Solution.AddProject(viewerProjectInfo).GetProject(viewerId);
            return csharpViewOfVbProject;
        }

        public static ProjectInfo ToProjectInfo(this Project project, ProjectId projectId, string projectName,
            CompilationOptions options, IEnumerable<ProjectReference> projectProjectReferences,
            ParseOptions parseOptions = null)
        {
            return ProjectInfo.Create(projectId, project.Version, projectName, project.AssemblyName,
                options.Language, null, project.OutputFilePath,
                options, parseOptions, System.Array.Empty<DocumentInfo>(), projectProjectReferences,
                project.MetadataReferences, project.AnalyzerReferences);
        }

        public static Project ToProjectFromAnyOptions(this Project project, CompilationOptions compilationOptions, ParseOptions parseOptions)
        {
            var projectInfo = project.ToProjectInfo(project.Id, project.Name, compilationOptions,
                project.ProjectReferences, parseOptions);
            var convertedSolution = project.Solution.RemoveProject(project.Id).AddProject(projectInfo);
            return convertedSolution.GetProject(project.Id);
        }

        public static string GetDirectoryPath(this Project proj)
        {
            string projectFilePath = proj.FilePath;
            if (projectFilePath != null) {
                return Path.GetDirectoryName(projectFilePath);
            }

            string solutionPath = GetDirectoryPath(proj);
            return proj.Documents
                .Where(d => d.FilePath != null && d.FilePath.StartsWith(solutionPath))
                .Select(d => d.FilePath.Replace(solutionPath, "").TrimStart(DirSeparators))
                .Where(p => p.IndexOfAny(DirSeparators) > -1)
                .Select(p => p.Split(DirSeparators).First())
                .OrderByDescending(p => p.Contains(proj.AssemblyName))
                .FirstOrDefault() ?? solutionPath;
        }

        public static string GetDirectoryPath(this Solution soln)
        {
            // Find a directory for projects that don't have a projectfile (e.g. websites) Current dir if in memory
            return soln.FilePath != null ? Path.GetDirectoryName(soln.FilePath) : Directory.GetCurrentDirectory();
        }

        public static (Project project, List<WipFileConversion<DocumentId>> firstPassDocIds)
            WithDocuments(this Project project, WipFileConversion<SyntaxNode>[] results)
        {
            var firstPassDocIds = results.Select(firstPassResult =>
            {
                DocumentId docId = null;
                if (firstPassResult.Wip != null)
                {
                    var document = project.AddDocument(firstPassResult.Path, firstPassResult.Wip,
                        filePath: firstPassResult.Path);
                    project = document.Project;
                    docId = document.Id;
                }

                return WipFileConversion.Create(firstPassResult.Path, docId, firstPassResult.Errors);
            }).ToList();

            //ToList ensures that the project returned has all documents added. We only return DocumentIds so it's easy to look up the final version of the doc later
            return (project, firstPassDocIds);
        }

        public static IEnumerable<WipFileConversion<Document>> GetDocuments(this Project project, List<WipFileConversion<DocumentId>> docIds)
        {
            return docIds.Select(f => WipFileConversion.Create(f.Path, f.Wip != null ? project.GetDocument(f.Wip) : null, f.Errors));
        }
    }
}