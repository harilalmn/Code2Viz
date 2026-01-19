using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Code2Viz.Editor
{
    /// <summary>
    /// Provides Call Hierarchy and Type Hierarchy functionality.
    /// </summary>
    public class HierarchyProvider
    {
        /// <summary>
        /// Gets the call hierarchy for a method at the specified position.
        /// </summary>
        public CallHierarchyResult? GetCallHierarchy(string code, int offset)
        {
            try
            {
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetRoot();

                // Find method at position
                var token = root.FindToken(offset);
                var methodDecl = token.Parent?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();

                if (methodDecl == null)
                {
                    // Check if we're on a method invocation
                    var invocation = token.Parent?.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
                    if (invocation != null)
                    {
                        // Get the method name from invocation
                        var methodName = GetMethodNameFromInvocation(invocation);
                        if (!string.IsNullOrEmpty(methodName))
                        {
                            // Find the method declaration
                            methodDecl = root.DescendantNodes()
                                .OfType<MethodDeclarationSyntax>()
                                .FirstOrDefault(m => m.Identifier.Text == methodName);
                        }
                    }
                }

                if (methodDecl == null)
                    return null;

                var methodName2 = methodDecl.Identifier.Text;
                var methodLine = tree.GetLineSpan(methodDecl.Identifier.Span).StartLinePosition.Line + 1;

                var result = new CallHierarchyResult
                {
                    MethodName = methodName2,
                    Line = methodLine,
                    IncomingCalls = new List<CallInfo>(),
                    OutgoingCalls = new List<CallInfo>()
                };

                // Find incoming calls (callers) - methods that call this method
                foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    var invokedName = GetMethodNameFromInvocation(invocation);
                    if (invokedName == methodName2)
                    {
                        // Find the containing method
                        var containingMethod = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                        if (containingMethod != null && containingMethod != methodDecl)
                        {
                            var callerLine = tree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1;
                            result.IncomingCalls.Add(new CallInfo
                            {
                                MethodName = containingMethod.Identifier.Text,
                                Line = callerLine,
                                IsIncoming = true
                            });
                        }
                    }
                }

                // Find outgoing calls (callees) - methods called by this method
                foreach (var invocation in methodDecl.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    var calledName = GetMethodNameFromInvocation(invocation);
                    if (!string.IsNullOrEmpty(calledName))
                    {
                        var callLine = tree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1;
                        result.OutgoingCalls.Add(new CallInfo
                        {
                            MethodName = calledName,
                            Line = callLine,
                            IsIncoming = false
                        });
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCallHierarchy error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the type hierarchy for a type at the specified position.
        /// </summary>
        public TypeHierarchyResult? GetTypeHierarchy(string code, int offset, IEnumerable<MetadataReference>? references = null)
        {
            try
            {
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetRoot();

                // Find type at position
                var token = root.FindToken(offset);
                var typeDecl = token.Parent?.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();

                if (typeDecl == null)
                {
                    // Check if we're on a type reference
                    var typeSyntax = token.Parent?.AncestorsAndSelf().OfType<IdentifierNameSyntax>().FirstOrDefault();
                    if (typeSyntax != null)
                    {
                        var typeName = typeSyntax.Identifier.Text;
                        typeDecl = root.DescendantNodes()
                            .OfType<TypeDeclarationSyntax>()
                            .FirstOrDefault(t => t.Identifier.Text == typeName);
                    }
                }

                if (typeDecl == null)
                    return null;

                var typeName2 = typeDecl.Identifier.Text;
                var typeLine = tree.GetLineSpan(typeDecl.Identifier.Span).StartLinePosition.Line + 1;

                var result = new TypeHierarchyResult
                {
                    TypeName = typeName2,
                    TypeKind = GetTypeKind(typeDecl),
                    Line = typeLine,
                    BaseTypes = new List<TypeInfo>(),
                    DerivedTypes = new List<TypeInfo>()
                };

                // Get base types
                if (typeDecl.BaseList != null)
                {
                    foreach (var baseType in typeDecl.BaseList.Types)
                    {
                        var baseTypeName = GetTypeName(baseType.Type);
                        result.BaseTypes.Add(new TypeInfo
                        {
                            TypeName = baseTypeName,
                            TypeKind = "Unknown", // Would need semantic model to determine
                            Line = 0 // Not in this file
                        });
                    }
                }

                // Find derived types (types that inherit from this type)
                foreach (var otherType in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    if (otherType == typeDecl) continue;
                    if (otherType.BaseList == null) continue;

                    foreach (var baseType in otherType.BaseList.Types)
                    {
                        var baseTypeName = GetTypeName(baseType.Type);
                        if (baseTypeName == typeName2)
                        {
                            var derivedLine = tree.GetLineSpan(otherType.Identifier.Span).StartLinePosition.Line + 1;
                            result.DerivedTypes.Add(new TypeInfo
                            {
                                TypeName = otherType.Identifier.Text,
                                TypeKind = GetTypeKind(otherType),
                                Line = derivedLine
                            });
                            break;
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTypeHierarchy error: {ex.Message}");
                return null;
            }
        }

        private string GetMethodNameFromInvocation(InvocationExpressionSyntax invocation)
        {
            return invocation.Expression switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
                _ => ""
            };
        }

        private string GetTypeName(TypeSyntax type)
        {
            return type switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                QualifiedNameSyntax qn => qn.Right.Identifier.Text,
                GenericNameSyntax gn => gn.Identifier.Text,
                _ => type.ToString()
            };
        }

        private string GetTypeKind(TypeDeclarationSyntax typeDecl)
        {
            return typeDecl switch
            {
                ClassDeclarationSyntax => "class",
                StructDeclarationSyntax => "struct",
                InterfaceDeclarationSyntax => "interface",
                RecordDeclarationSyntax => "record",
                _ => "type"
            };
        }
    }

    public class CallHierarchyResult
    {
        public string MethodName { get; set; } = "";
        public int Line { get; set; }
        public List<CallInfo> IncomingCalls { get; set; } = new();
        public List<CallInfo> OutgoingCalls { get; set; } = new();
    }

    public class CallInfo
    {
        public string MethodName { get; set; } = "";
        public int Line { get; set; }
        public bool IsIncoming { get; set; }
    }

    public class TypeHierarchyResult
    {
        public string TypeName { get; set; } = "";
        public string TypeKind { get; set; } = "";
        public int Line { get; set; }
        public List<TypeInfo> BaseTypes { get; set; } = new();
        public List<TypeInfo> DerivedTypes { get; set; } = new();
    }

    public class TypeInfo
    {
        public string TypeName { get; set; } = "";
        public string TypeKind { get; set; } = "";
        public int Line { get; set; }
    }
}
