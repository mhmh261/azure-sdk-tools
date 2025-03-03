// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using APIView;
using APIView.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApiView
{
    public class CodeFileRenderer
    {
        public static CodeFileRenderer Instance = new CodeFileRenderer();

        public RenderResult Render(CodeFile file, bool showDocumentation = false, bool enableSkipDiff = false)
        {
            var codeLines = new List<CodeLine>();
            var sections = new Dictionary<int,TreeNode<CodeLine>>();
            Render(codeLines, file.Tokens, showDocumentation, enableSkipDiff, sections);
            return new RenderResult(codeLines.ToArray(), sections);
        }

        public CodeLine[] Render(CodeFileToken[] tokens, bool showDocumentation = false, bool enableSkipDiff = false)
        {
            var codeLines = new List<CodeLine>();
            var sections = new Dictionary<int, TreeNode<CodeLine>>();
            Render(codeLines, tokens, showDocumentation, enableSkipDiff, sections);
            return codeLines.ToArray();
        }

        private void Render(List<CodeLine> list, IEnumerable<CodeFileToken> node, bool showDocumentation, bool enableSkipDiff, Dictionary<int,TreeNode<CodeLine>> sections)
        {
            var stringBuilder = new StringBuilder();
            string currentId = null;
            bool isDocumentationRange = false;
            bool isHiddenApiToken = false;
            bool isDeprecatedToken = false;
            bool isSkipDiffRange = false;
            Stack<SectionType> nodesInProcess = new Stack<SectionType>();
            int lineNumber = 0;
            (int Count, int Curr) tableColumnCount = (0, 0);
            (int Count, int Curr) tableRowCount = (0, 0);
            TreeNode<CodeLine> section = null;
            int leafSectionPlaceHolderNumber = 0;

            foreach (var token in node)
            {
                // Skip all tokens till range end
                if (enableSkipDiff && isSkipDiffRange && token.Kind != CodeFileTokenKind.SkipDiffRangeEnd)
                    continue;

                if (!showDocumentation && isDocumentationRange && token.Kind != CodeFileTokenKind.DocumentRangeEnd)
                    continue;

                switch (token.Kind)
                {
                    case CodeFileTokenKind.Newline:
                        int ? sectionKey = (nodesInProcess.Count > 0 && section == null) ? sections.Count: null;
                        CodeLine codeLine = new CodeLine(stringBuilder.ToString(), currentId, String.Empty, ++lineNumber, sectionKey, isDocumentation: isDocumentationRange, isHiddenApi: isHiddenApiToken);
                        if (leafSectionPlaceHolderNumber != 0)
                        {
                            lineNumber += leafSectionPlaceHolderNumber - 1;
                            leafSectionPlaceHolderNumber = 0;
                        }
                        if (nodesInProcess.Count > 0)
                        {
                            if (nodesInProcess.Peek().Equals(SectionType.Heading))
                            {
                                if (section == null)
                                {
                                    section = new TreeNode<CodeLine>(codeLine);
                                    list.Add(codeLine);
                                }
                                else
                                {
                                    section = section.AddChild(codeLine);
                                }
                            }
                            else
                            {
                                section.AddChild(codeLine);
                            }
                        }
                        else
                        {
                            if (section != null)
                            {
                                sections.Add(sections.Count, section);
                                section = null;
                            }
                            list.Add(codeLine);
                        }
                        currentId = null;
                        stringBuilder.Clear();
                        break;

                    case CodeFileTokenKind.DocumentRangeStart:
                        StartDocumentationRange(stringBuilder);
                        isDocumentationRange = true;
                        break;

                    case CodeFileTokenKind.DocumentRangeEnd:
                        CloseDocumentationRange(stringBuilder);
                        isDocumentationRange = false;
                        break;

                    case CodeFileTokenKind.HiddenApiRangeStart:
                        isHiddenApiToken = true;
                        break;

                    case CodeFileTokenKind.HiddenApiRangeEnd:
                        isHiddenApiToken = false;
                        break;

                    case CodeFileTokenKind.DeprecatedRangeStart:
                        isDeprecatedToken = true;
                        break;

                    case CodeFileTokenKind.DeprecatedRangeEnd:
                        isDeprecatedToken = false;
                        break;

                    case CodeFileTokenKind.SkipDiffRangeStart:
                        isSkipDiffRange = true;
                        break;

                    case CodeFileTokenKind.SkipDiffRangeEnd:
                        isSkipDiffRange = false;
                        break;

                    case CodeFileTokenKind.FoldableSectionHeading:
                        nodesInProcess.Push(SectionType.Heading);
                        currentId = (token.DefinitionId != null) ? token.DefinitionId : currentId;
                        RenderToken(token, stringBuilder, isDeprecatedToken, isHiddenApiToken);
                        break;

                    case CodeFileTokenKind.FoldableSectionContentStart:
                        nodesInProcess.Push(SectionType.Content);
                        break;

                    case CodeFileTokenKind.FoldableSectionContentEnd:
                        section = (section.IsRoot) ? section : section.Parent;
                        nodesInProcess.Pop();
                        if (nodesInProcess.Peek().Equals(SectionType.Heading))
                        {
                            nodesInProcess.Pop();
                        }
                        if (nodesInProcess.Count == 0 && section != null)
                        {
                            sections.Add(sections.Count, section);
                            section = null;
                        }
                        break;

                    case CodeFileTokenKind.TableBegin:
                        stringBuilder.Append("<table class=\"table table-sm\">");
                        currentId = (token.DefinitionId != null) ? token.DefinitionId : currentId;
                        break;

                    case CodeFileTokenKind.TableColumnCount:
                        tableColumnCount.Count = Convert.ToInt16(token.Value);
                        tableColumnCount.Curr = 0;
                        break;

                    case CodeFileTokenKind.TableRowCount:
                        tableRowCount.Count = Convert.ToInt16(token.Value);
                        tableRowCount.Curr = 0;
                        break;

                    case CodeFileTokenKind.TableColumnName:
                        if (tableColumnCount.Curr == 0)
                        {
                            stringBuilder.Append($"<thead><tr>");
                            stringBuilder.Append($"<th height=\"30\" scope=\"col\">");
                            RenderToken(token, stringBuilder, isDeprecatedToken, isHiddenApiToken);
                            stringBuilder.Append("</th>");
                            tableColumnCount.Curr++;
                        }
                        else if (tableColumnCount.Curr == tableColumnCount.Count - 1)
                        {
                            stringBuilder.Append($"<th height=\"30\" scope=\"col\">");
                            RenderToken(token, stringBuilder, isDeprecatedToken, isHiddenApiToken);
                            stringBuilder.Append("</th>");
                            stringBuilder.Append("</tr></thead><tbody>");
                            tableColumnCount.Curr = 0;
                        }
                        else
                        {
                            stringBuilder.Append($"<th height=\"30\" scope=\"col\">");
                            RenderToken(token, stringBuilder, isDeprecatedToken, isHiddenApiToken);
                            stringBuilder.Append("</th>");
                            tableColumnCount.Curr++;
                        }
                        break;

                    case CodeFileTokenKind.TableCellBegin:
                        if (tableColumnCount.Curr == 0)
                        {
                            var rowId = $"{currentId}-tr-{tableRowCount.Curr + 1}";
                            stringBuilder.Append($"<tr data-inline-id=\"{rowId}\"><td height=\"30\"><a class=\"line-comment-button\">+</a>");
                            tableColumnCount.Curr++;
                        }
                        else if (tableColumnCount.Curr == tableColumnCount.Count - 1)
                        {
                            stringBuilder.Append($"<td height=\"30\">");
                            tableColumnCount.Curr = 0;
                            tableRowCount.Curr++;
                        }
                        else
                        {
                            stringBuilder.Append($"<td height=\"30\">");
                            tableColumnCount.Curr++;
                        }
                        break;

                    case CodeFileTokenKind.TableCellEnd:
                        stringBuilder.Append($"</td height=\"30\">");
                        if (tableColumnCount.Curr == 0)
                        {
                            stringBuilder.Append($"</tr>");
                        }
                        break;

                    case CodeFileTokenKind.TableEnd:
                        stringBuilder.Append($"</tbody>");
                        stringBuilder.Append("</table>");

                        break;

                    case CodeFileTokenKind.LeafSectionPlaceholder:
                        stringBuilder.Append(token.Value);
                        leafSectionPlaceHolderNumber = (int)token.NumberOfLinesinLeafSection;
                        break;

                    case CodeFileTokenKind.ExternalLinkStart:
                        stringBuilder.Append($"<a target=\"_blank\" href=\"{token.Value}\">");
                        break;

                    case CodeFileTokenKind.ExternalLinkEnd:
                        stringBuilder.Append("</a>");
                        break;

                    default:
                        currentId = (token.DefinitionId != null) ? token.DefinitionId : currentId;
                        RenderToken(token, stringBuilder, isDeprecatedToken, isHiddenApiToken);
                        break;
                }
            }
        }

        protected virtual void RenderToken(CodeFileToken token, StringBuilder stringBuilder, bool isDeprecatedToken, bool isHiddenApiToken)
        {
            if (token.Value != null)
            {
                stringBuilder.Append(token.Value);
            }
        }

        // Below two methods are HTML renderer specific and implemented in htmlrender class
        // These methods should not render anything for text renderer so keeping it empty
        protected virtual void StartDocumentationRange(StringBuilder stringBuilder) { }
        protected virtual void CloseDocumentationRange(StringBuilder stringBuilder) { }

        private void UpdateDefinitionId(CodeFileToken token, string currentId)
        {
            currentId = (token.DefinitionId != null) ? token.DefinitionId : currentId;
        }
    }

    public struct RenderResult
    {
        public RenderResult(CodeLine[] codeLines, Dictionary<int,TreeNode<CodeLine>> sections)
        {
            CodeLines = codeLines;
            Sections = sections;
        }

        public CodeLine[] CodeLines { get; }
        public Dictionary<int, TreeNode<CodeLine>> Sections { get; }
    }

    enum SectionType
    {
        Heading,
        Content
    }
}
