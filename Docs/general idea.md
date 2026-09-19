ai compiler analysis

Yes, a code analyzer can absolutely be used to catch design pattern overuse and over-engineering. While a compiler doesn't care if an LLM wrote five factories and three interfaces just to print "Hello World", a Roslyn analyzer can see the structural footprint left by over-patterned code. [1, 2, 3]
LLMs are prone to "speculative over-engineering"—building highly abstract architectures for simple tasks because their training data contains complex design patterns. [3, 4]
You can catch and reject these anti-patterns using structural heuristics via the Roslyn AST.

1. Structural Signs of Pattern Overuse (And How to Code Them)
   Over-engineering always leaves specific clues in the code's syntax tree. Here are four major anti-patterns you can programmatically detect and use to correct the LLM: [5]
   Anti-Pattern A: The "Strategy Pattern Without a Strategy"

-
- The Bad Practice: The LLM creates an interface, a concrete class implementing that interface, and a factory—all to handle one single piece of logic that never changes. [1]
- The Roslyn Check: Scan for interfaces that have exactly one concrete implementation in the generated syntax tree.[1]
- The Feedback to LLM: "You created IUserService and UserService, but there is only one implementation. Remove the interface and instantiate the class directly unless dynamic runtime variation is required." [1]
  Anti-Pattern B: "Speculative Factory Abuse"
- The Bad Practice: The LLM creates an abstract factory pattern to instantiate a single object that could have just used a simple new() constructor. [1]
- The Roslyn Check: Look for classes ending in Factory where the factory method contains zero conditional logic (if or switch) and just returns a single hardcoded class type. [1]
- The Feedback to LLM: "Your factory class does not contain branching logic to justify its existence. Replace it with a direct constructor call." [1]
  Anti-Pattern C: Deep Indirection (The "Pass-Through" Class)
- The Bad Practice: The LLM chains together a Facade, an Adapter, and a Service. Class A calls Class B, which calls Class C, and none of them actually transform the data—they just pass it along.
- The Roslyn Check: Measure the Indirection-to-Logic Ratio. In a class or method, count how many statements are purely forwarding a call to an identical signature in an underlying dependency.
- The Feedback to LLM: "You have introduced unnecessary indirection. Method X simply passes data to Method Y without adding value or modifying state. Flatten this pipeline." [3]
  Anti-Pattern D: Misuse of Singletons (The Hidden Global State)
- The Bad Practice: Overusing the Singleton pattern to pass state across a system, creating hard-to-test "God Objects".
- The Roslyn Check: Scan for classes exposing a public static instance of themselves (e.g., public static MyClass Instance { get; }).
- The Feedback to LLM: "Do not use the Singleton pattern for shared state. Use standard Dependency Injection to register this class as a singleton or scoped service instead." [1, 6]

2. A Roslyn Implementation: Catching "Useless Interfaces"
   This custom Roslyn walker flags when the LLM over-engineers code by creating single-use abstractions that add cognitive overhead without flexibility. [3]
   using System.Linq;
   using Microsoft.CodeAnalysis;
   using Microsoft.CodeAnalysis.CSharp;
   using Microsoft.CodeAnalysis.CSharp.Syntax;
   using System.Collections.Generic;

public class OverEngineeringAnalyzer : CSharpSyntaxWalker
{
public List<string> OverEngineeredWarnings { get; } = new();

    public override void VisitCompilationUnit(CompilationUnitSyntax node)
    {
        // Find all interfaces declared in the code snippet
        var interfaces = node.DescendantNodes().OfType<InterfaceDeclarationSyntax>().ToList();

        // Find all classes declared in the code snippet
        var classes = node.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();

        foreach (var @interface in interfaces)
        {
            string interfaceName = @interface.Identifier.Text;

            // Count how many classes implement this interface in the generated snippet
            int implementationCount = classes.Count(c =>
                c.BaseList?.Types.Any(t => t.Type.ToString() == interfaceName) == true);

            // Rule: If an interface has exactly 1 implementation, it's often over-engineered
            // for short LLM script generation tasks.
            if (implementationCount == 1)
            {
                var implClass = classes.First(c => c.BaseList.Types.Any(t => t.Type.ToString() == interfaceName));

                OverEngineeredWarnings.Add(
                    $"[ANTI-PATTERN] Overuse of Abstraction: Interface '{interfaceName}' has exactly one " +
                    $"implementation ('{implClass.Identifier.Text}'). Unless polymorphism is strictly required, " +
                    $"remove the interface and use the concrete class directly to simplify the design."
                );
            }
        }

        base.VisitCompilationUnit(node);
    }

}

3. Combining Metrics for an "Over-Engineering Score"
   To catch complex pattern stacking (like mixing Decorators, Proxies, and Adapters), you can look at the ratio of Architecture to Logic: [3]
   $$\text{Over-Engineering Ratio} = \frac{\text{Number of Types (Interfaces + Classes + Structural Methods)}}{\text{Number of Total Execution Statements}}$$
   If an LLM writes 120 lines of architectural scaffolding (constructors, wrappers, empty interfaces) to execute 5 lines of business logic, the ratio will spike. You can set a rule in your evaluation loop: If the Over-Engineering Ratio exceeds 2.5, reject the code and tell the LLM to simplify.
   How this forces the LLM to write cleaner code
   LLMs are highly responsive to constraints. When a Roslyn analyzer rejects a snippet with a message like:
   "Rejected: You created 4 classes and 2 interfaces to solve a problem that requires less than 20 lines of logic. Rewrite this using a single, cohesive class and flat methods."
   The LLM immediately strips away the decorative boilerplate and generates a highly maintainable, direct solution. [7]
   Would you like to explore how to write an automated prompt loop that feeds these architectural violations back to the LLM, or do you have a specific design pattern you see your LLM overusing frequently?

[1] https://medium.com
[2] https://testrigor.com
[3] https://www.quora.com
[4] https://www.youtube.com
[5] https://codescene.com
[6] https://medium.com
[7] https://www.geeksforgeeks.org

Effective code metrics that can be validated using a Roslyn Analyzer include structural complexity, code nesting, and coupling thresholds that you can check during design time. [1, 2, 3]
Cyclomatic Complexity

- What it measures: The number of independent execution paths through a method (counting if, while, for, case statements). [3]
- Roslyn validation: A syntax node action (RegisterSyntaxNodeAction) can inspect method declarations, count branching tokens or statements via the Syntax Tree, and trigger a warning if the count exceeds a threshold (e.g., > 10). [4, 5]
  Cognitive Complexity

- What it measures: How hard a piece of code is to intuitively understand, penalizing nested structures more heavily than flat sequential branches. [2]
- Roslyn validation: Analyzers traverse the syntax tree recursively, incrementing a penalty score whenever nesting constructs (like an if inside a for) are encountered.
  Maximum Depth of Nesting

- What it measures: The deepest level of indented control flow blocks within a method. [2, 3]
- Roslyn validation: By tracking the parent-child depth of block statements (BlockSyntax) relative to a method declaration, an analyzer can flag methods exceeding a specified depth (e.g., 4 levels). [4]
  Class Coupling (Coupling Between Objects - CBO)

- What it measures: The number of distinct types/classes a specific class or method interacts with or depends on. [2, 3]
- Roslyn validation: Using the semantic model (SemanticModel), an analyzer can evaluate types referenced within member declarations, parameter lists, and local instantiations to flag overly coupled components. [4]
  Lines of Code (LOC) / Method Length

- What it measures: The physical or executable size of a method or type. [3, 6]
- Roslyn validation: An analyzer can check the span of a MethodDeclarationSyntax by comparing the line number of its starting and ending tokens, reporting diagnostics if it crosses an acceptable limit.
  If you want, I can share:

- A code snippet for a custom Roslyn analyzer that calculates one of these metrics.
- How to configure these metric rules using an EditorConfig file.

[1] https://learn.microsoft.com
[2] https://moderne.ai
[3] https://learn.microsoft.com
[4] https://learn.microsoft.com
[5] https://dev.to
[6] https://www.ndepend.com

extractions

Anti-Pattern A: The "Strategy Pattern Without a Strategy"

-
- The Bad Practice: The LLM creates an interface, a concrete class implementing that interface, and a factory—all to handle one single piece of logic that never changes. [1]
- The Roslyn Check: Scan for interfaces that have exactly one concrete implementation in the generated syntax tree.[1]
- The Feedback to LLM: "You created IUserService and UserService, but there is only one implementation. Remove the interface and instantiate the class directly unless dynamic runtime variation is required." [1]
  Anti-Pattern B: "Speculative Factory Abuse"
- The Bad Practice: The LLM creates an abstract factory pattern to instantiate a single object that could have just used a simple new() constructor. [1]
- The Roslyn Check: Look for classes ending in Factory where the factory method contains zero conditional logic (if or switch) and just returns a single hardcoded class type. [1]
- The Feedback to LLM: "Your factory class does not contain branching logic to justify its existence. Replace it with a direct constructor call." [1]
  Anti-Pattern C: Deep Indirection (The "Pass-Through" Class)
- The Bad Practice: The LLM chains together a Facade, an Adapter, and a Service. Class A calls Class B, which calls Class C, and none of them actually transform the data—they just pass it along.
- The Roslyn Check: Measure the Indirection-to-Logic Ratio. In a class or method, count how many statements are purely forwarding a call to an identical signature in an underlying dependency.
- The Feedback to LLM: "You have introduced unnecessary indirection. Method X simply passes data to Method Y without adding value or modifying state. Flatten this pipeline." [3]
  Anti-Pattern D: Misuse of Singletons (The Hidden Global State)
- The Bad Practice: Overusing the Singleton pattern to pass state across a system, creating hard-to-test "God Objects".
- The Roslyn Check: Scan for classes exposing a public static instance of themselves (e.g., public static MyClass Instance { get; }).
- The Feedback to LLM: "Do not use the Singleton pattern for shared state. Use standard Dependency Injection to register this class as a singleton or scoped service instead." [1, 6]

Cyclomatic Complexity

- What it measures: The number of independent execution paths through a method (counting if, while, for, case statements). [3]
- Roslyn validation: A syntax node action (RegisterSyntaxNodeAction) can inspect method declarations, count branching tokens or statements via the Syntax Tree, and trigger a warning if the count exceeds a threshold (e.g., > 10). [4, 5]
  Cognitive Complexity

- What it measures: How hard a piece of code is to intuitively understand, penalizing nested structures more heavily than flat sequential branches. [2]
- Roslyn validation: Analyzers traverse the syntax tree recursively, incrementing a penalty score whenever nesting constructs (like an if inside a for) are encountered.
  Maximum Depth of Nesting

- What it measures: The deepest level of indented control flow blocks within a method. [2, 3]
- Roslyn validation: By tracking the parent-child depth of block statements (BlockSyntax) relative to a method declaration, an analyzer can flag methods exceeding a specified depth (e.g., 4 levels). [4]
  Class Coupling (Coupling Between Objects - CBO)

- What it measures: The number of distinct types/classes a specific class or method interacts with or depends on. [2, 3]
- Roslyn validation: Using the semantic model (SemanticModel), an analyzer can evaluate types referenced within member declarations, parameter lists, and local instantiations to flag overly coupled components. [4]
  Lines of Code (LOC) / Method Length

- What it measures: The physical or executable size of a method or type. [3, 6]
- Roslyn validation: An analyzer can check the span of a MethodDeclarationSyntax by comparing the line number of its starting and ending tokens, reporting diagnostics if it crosses an acceptable limit.
