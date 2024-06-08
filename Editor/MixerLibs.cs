using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Maranara.Marrow
{
    public static class MixerLibs
    {
        private static MethodReference GetPtrConstructor(TypeDefinition type, ModuleDefinition rootModule)
        {
            foreach (var method in type.Methods)
            {
                if (method.Name == ".ctor" && method.Parameters.Count == 1 && method.Parameters[0].ParameterType.FullName == "System.IntPtr")
                {
                    return rootModule.ImportReference(method);
                }
            }

            return null;
        }
        public static bool CheckParentType(TypeDefinition type)
        {
            if (type.FullName == "UnityEngine.MonoBehaviour")
            {
                return true;
            }

            if (type.BaseType == null)
            {
                return false;
            }

            var res = type.BaseType.Resolve();
            if (res == null)
            {
                return false;
            }

            return CheckParentType(res);
        }
        public static MethodReference GetOrAddPtrConstructorWithinAssembly(TypeDefinition type, ModuleDefinition rootModule, bool hasLeft = false)
        {
            var reference = GetPtrConstructor(type, rootModule);
            if (reference != null)
            {
                return reference;
            }

            if (hasLeft)
            {
                return null;
            }

            var baseType = type.BaseType.Resolve();
            var baseConstructor = GetOrAddPtrConstructorWithinAssembly(baseType, rootModule, hasLeft: type.Scope.Name != baseType.Scope.Name);

            var methodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            var method = new MethodDefinition(".ctor", methodAttributes, type.Module.TypeSystem.Void);
            method.Parameters.Add(new ParameterDefinition("ptr", ParameterAttributes.None, type.Module.TypeSystem.IntPtr));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, baseConstructor));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            type.Methods.Add(method);

            return method;
        }

        public static ClassDeclarationSyntax UpdateMainClass(CompilationUnitSyntax root, string @class)
        {
            return (ClassDeclarationSyntax)(root
            .ChildNodes().First().ChildNodes()
            .FirstOrDefault((c)
            => c.GetType() == typeof(ClassDeclarationSyntax)
            && ((ClassDeclarationSyntax)c).Identifier.ToString() == @class
            ) ?? root
            .ChildNodes()
            .FirstOrDefault((c)
            => c.GetType() == typeof(ClassDeclarationSyntax)
            && ((ClassDeclarationSyntax)c).Identifier.ToString() == @class
            ));
        }

        // https://stackoverflow.com/a/37743242
        public static MethodDeclarationSyntax GetMethodDeclarationSyntax(string returnTypeName, string methodName, string[] parameterTypes, string[] paramterNames)
        {
            var parameterList = ParameterList(SeparatedList(GetParametersList(parameterTypes, paramterNames)));
            return MethodDeclaration(attributeLists: List<AttributeListSyntax>(),
                          modifiers: TokenList(),
                          returnType: ParseTypeName(returnTypeName),
                          explicitInterfaceSpecifier: null,
                          identifier: Identifier(methodName),
                          typeParameterList: null,
                          parameterList: parameterList,
                          constraintClauses: List<TypeParameterConstraintClauseSyntax>(),
                          body: null,
                          semicolonToken: Token(SyntaxKind.SemicolonToken));

            IEnumerable<ParameterSyntax> GetParametersList(string[] parameterTypesToGet, string[] paramterNamesToGet)
            {
                for (int i = 0; i < parameterTypesToGet.Length; i++)
                {
                    yield return Parameter(attributeLists: List<AttributeListSyntax>(),
                                                             modifiers: TokenList(),
                                                             type: ParseTypeName(parameterTypesToGet[i]),
                                                             identifier: Identifier(paramterNamesToGet[i]),
                                                             @default: null);
                }
            }
        }

        public static string MakeAsmSafe(string _str)
        {
            string str = _str;
            // this wont work in some specific edge cases but for the most part it should be fine
            foreach (char c in Path.GetInvalidFileNameChars())
                str = str.Replace(c, '_');

            str = str.Trim('_');
            return str;
        }

        // modified from https://gist.github.com/xdaDaveShaw/87643170e5fa97b7da3b
        public class AttributeRemoverRewriter : CSharpSyntaxRewriter
        {
            public AttributeRemoverRewriter()
            {
            }

            public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
            {
                var newAttributes = new SyntaxList<AttributeListSyntax>();

                foreach (var attributeList in node.AttributeLists)
                {
                    var nodesToRemove =
                        attributeList
                        .Attributes
                        .ToArray();

                    if (nodesToRemove.Length != attributeList.Attributes.Count)
                    {
                        //We want to remove only some of the attributes
                        var newAttribute =
                            (AttributeListSyntax)VisitAttributeList(
                                attributeList.RemoveNodes(nodesToRemove, SyntaxRemoveOptions.KeepNoTrivia));

                        newAttributes = newAttributes.Add(newAttribute);
                    }
                }

                //Get the leading trivia (the newlines and comments)
                var leadTriv = node.GetLeadingTrivia();
                node = node.WithAttributeLists(newAttributes);

                //Append the leading trivia to the method
                node = node.WithLeadingTrivia(leadTriv);
                return node;
            }

            public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                var newAttributes = new SyntaxList<AttributeListSyntax>();

                foreach (var attributeList in node.AttributeLists)
                {
                    var nodesToRemove =
                        attributeList
                        .Attributes
                        .ToArray();

                    if (nodesToRemove.Length != attributeList.Attributes.Count)
                    {
                        //We want to remove only some of the attributes
                        var newAttribute =
                            (AttributeListSyntax)VisitAttributeList(
                                attributeList.RemoveNodes(nodesToRemove, SyntaxRemoveOptions.KeepNoTrivia));

                        newAttributes = newAttributes.Add(newAttribute);
                    }
                }

                //Get the leading trivia (the newlines and comments)
                var leadTriv = node.GetLeadingTrivia();
                node = node.WithAttributeLists(newAttributes);

                //Append the leading trivia to the method
                node = node.WithLeadingTrivia(leadTriv);
                return node;
            }
        }
    }
}
