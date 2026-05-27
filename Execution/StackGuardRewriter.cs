using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Code2Viz.Execution;

/// <summary>
/// Rewrites user code to inject a stack-depth guard at the top of every method-like body.
/// <c>System.Runtime.CompilerServices.RuntimeHelpers.EnsureSufficientExecutionStack()</c> throws a
/// <b>catchable</b> <see cref="InsufficientExecutionStackException"/> just before the stack would
/// actually overflow. That turns runaway recursion in user code (e.g. mutual recursion between two
/// methods) into an ordinary exception the host's <c>catch (Exception)</c> handles — instead of an
/// <b>uncatchable</b> <see cref="StackOverflowException"/>, which the .NET runtime fails-fast on,
/// killing the whole process.
///
/// Shared by both compilers: Code2Viz's <c>ModuleCompiler</c> (Main() + sketch mode) and Animator's
/// <c>SketchCompiler</c> (linked into Animator.csproj — namespace stays <c>Code2Viz.Execution</c>).
/// </summary>
/// <remarks>
/// The guard is injected into every named member body (methods, local functions, constructors,
/// operators, accessors, expression-bodied properties/indexers). Any recursive cycle must pass
/// through at least one of these, so guarding them all catches every practical case. Lambdas and
/// anonymous methods are deliberately left untouched: rewriting an expression-bodied lambda into a
/// statement lambda can change overload resolution (Func vs. Expression), and recursion that flows
/// solely through anonymous functions without any named member is not a real-world scenario.
///
/// The injected statement carries no trivia, so it shares a line with the opening brace and the
/// original statements keep their source line numbers — runtime stack traces still map to the
/// user's file. (Character offsets within a line do shift, so this must NOT be applied to trees
/// used for offset-based editor features like go-to-definition or rename — only to the execute
/// path.)
/// </remarks>
public sealed class StackGuardRewriter : CSharpSyntaxRewriter
{
    // Fully-qualified + global:: so the call needs no using directive and can never be shadowed by
    // a type the user happens to declare.
    private const string GuardText =
        "global::System.Runtime.CompilerServices.RuntimeHelpers.EnsureSufficientExecutionStack();";

    private static StatementSyntax Guard =>
        SyntaxFactory.ParseStatement(GuardText)
            .WithLeadingTrivia()
            .WithTrailingTrivia();

    /// <summary>Convenience entry point: rewrite a whole tree's root.</summary>
    public static SyntaxNode Inject(SyntaxNode root) => new StackGuardRewriter().Visit(root);

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var n = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
        if (n.Body != null)
            return n.WithBody(PrependGuard(n.Body));
        if (n.ExpressionBody != null)
            return n.WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithBody(GuardedBlock(n.ExpressionBody, IsVoid(n.ReturnType)));
        return n; // abstract / partial / extern — no body to guard
    }

    public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        var n = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!;
        if (n.Body != null)
            return n.WithBody(PrependGuard(n.Body));
        if (n.ExpressionBody != null)
            return n.WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithBody(GuardedBlock(n.ExpressionBody, IsVoid(n.ReturnType)));
        return n;
    }

    public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        var n = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;
        if (n.Body != null)
            return n.WithBody(PrependGuard(n.Body));
        if (n.ExpressionBody != null)
            return n.WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithBody(GuardedBlock(n.ExpressionBody, isVoid: true));
        return n;
    }

    public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node)
    {
        var n = (OperatorDeclarationSyntax)base.VisitOperatorDeclaration(node)!;
        if (n.Body != null)
            return n.WithBody(PrependGuard(n.Body));
        if (n.ExpressionBody != null)
            return n.WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithBody(GuardedBlock(n.ExpressionBody, isVoid: false)); // operators return a value
        return n;
    }

    public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
    {
        var n = (ConversionOperatorDeclarationSyntax)base.VisitConversionOperatorDeclaration(node)!;
        if (n.Body != null)
            return n.WithBody(PrependGuard(n.Body));
        if (n.ExpressionBody != null)
            return n.WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithBody(GuardedBlock(n.ExpressionBody, isVoid: false));
        return n;
    }

    public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
    {
        var n = (AccessorDeclarationSyntax)base.VisitAccessorDeclaration(node)!;
        // Only a getter yields a value; set/init/add/remove are void-like.
        bool isVoid = !n.IsKind(SyntaxKind.GetAccessorDeclaration);
        if (n.Body != null)
            return n.WithBody(PrependGuard(n.Body));
        if (n.ExpressionBody != null)
            return n.WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithBody(GuardedBlock(n.ExpressionBody, isVoid));
        return n; // auto-property accessor (`get;`) — nothing to guard
    }

    public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        var n = (PropertyDeclarationSyntax)base.VisitPropertyDeclaration(node)!;
        // `int X => expr;` shorthand has its arrow on the property itself; promote to a getter block.
        if (n.ExpressionBody != null)
            return n.WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithAccessorList(GetterFrom(n.ExpressionBody, IsVoid(n.Type)));
        return n; // accessor list (if any) is handled by VisitAccessorDeclaration
    }

    public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
    {
        var n = (IndexerDeclarationSyntax)base.VisitIndexerDeclaration(node)!;
        if (n.ExpressionBody != null)
            return n.WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithAccessorList(GetterFrom(n.ExpressionBody, IsVoid(n.Type)));
        return n;
    }

    // ---- helpers -----------------------------------------------------------

    private static BlockSyntax PrependGuard(BlockSyntax body)
        => body.WithStatements(body.Statements.Insert(0, Guard));

    private static BlockSyntax GuardedBlock(ArrowExpressionClauseSyntax arrow, bool isVoid)
        => SyntaxFactory.Block(Guard, ExpressionToStatement(arrow.Expression, isVoid));

    private static AccessorListSyntax GetterFrom(ArrowExpressionClauseSyntax arrow, bool isVoid)
    {
        var getter = SyntaxFactory
            .AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
            .WithBody(GuardedBlock(arrow, isVoid));
        return SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(getter));
    }

    private static StatementSyntax ExpressionToStatement(ExpressionSyntax expr, bool isVoid)
    {
        // `=> throw ...` can't become `return throw ...;` — it must stay a throw statement.
        if (expr is ThrowExpressionSyntax t)
            return SyntaxFactory.ThrowStatement(t.Expression);
        return isVoid
            ? SyntaxFactory.ExpressionStatement(expr)
            : SyntaxFactory.ReturnStatement(expr);
    }

    private static bool IsVoid(TypeSyntax type)
        => type is PredefinedTypeSyntax p && p.Keyword.IsKind(SyntaxKind.VoidKeyword);
}
