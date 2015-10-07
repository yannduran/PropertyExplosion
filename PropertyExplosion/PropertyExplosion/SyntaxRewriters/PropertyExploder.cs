﻿using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;

namespace PropertyExplosion.SyntaxRewriters
{
    /// <summary>
    /// Syntax Rewriter to EXPLODE properties from Auto into Full-Properties
    /// </summary>
    public class PropertyExploder : CSharpSyntaxRewriter
    {
        private readonly SyntaxNode _propertyParent;
        private readonly PropertyDeclarationSyntax _autoProperty;

        #region Constructors

        public PropertyExploder(SyntaxNode propertyParent, PropertyDeclarationSyntax autoProperty)
        {
            this._propertyParent = propertyParent;
            this._autoProperty = autoProperty;
        }

        #endregion

        #region Properties

        private string _privateFieldName;
        private string PrivateFieldName
        {
            get
            {
                if (String.IsNullOrWhiteSpace(this._privateFieldName))
                {
                    // Take the name of the property, make first letter lower case and prefix with _ e.g. "MyProperty" becomes "_myProperty".
                    this._privateFieldName = String.Format("_{0}{1}", Char.ToLowerInvariant(this._autoProperty.Identifier.ValueText[0]), this._autoProperty.Identifier.ValueText.Substring(1));
                }
                return this._privateFieldName;
            }
        }

        #endregion

        public override SyntaxNode Visit(SyntaxNode node)
        {
            // Check each node until we find the property's parent. When  we've found it, let's add a private field to it
            if (_propertyParent.IsEquivalentTo(node))
            {
                // Create the private "backing field" of the property
                var privateField = SyntaxFactory.FieldDeclaration(
                                        SyntaxFactory.VariableDeclaration(_autoProperty.Type, // Property Type
                                            SyntaxFactory.SeparatedList(
                                                new[] { SyntaxFactory.VariableDeclarator(
                                                    SyntaxFactory.Identifier(this.PrivateFieldName)) }))) // Field Name
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)); // Modifier (always private)

                // Insert the backing field just ahead of the original property
                var newParent = node.InsertNodesBefore(_autoProperty, new[] { privateField });

                return base.Visit(newParent); // Return the new parent which replaces the original.
            }
            return base.Visit(node);
        }

        public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax property)
        {
            // Check each node until we find the property in question. When we have it, we'll replace it with a Full Property
            if (property.IsEquivalentTo(this._autoProperty))
            {
                AccessorDeclarationSyntax getter = null;
                AccessorDeclarationSyntax setter = null;

                if (property.HasGetter()) // Check if orignial Auto Property had a getter
                {
                    // Create a new Getter with a body, returning a private field
                    getter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithBody(
                                    SyntaxFactory.Block(
                                        SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName(this.PrivateFieldName))) // return field e.g. return _myProperty;
                                 )
                                 .WithModifiers(property.GetGetter().Modifiers) // Keep orignial modifiers
                                 .WithoutTrivia();
                }
                if (property.HasSetter()) // Check if orignial Auto Property had a setter
                {
                    // Create a new Setter with a body, setter the private field
                    setter = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithBody(
                                SyntaxFactory.Block(
                                    SyntaxFactory.ExpressionStatement(
                                        SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName(this.PrivateFieldName), SyntaxFactory.IdentifierName("value")))) // Assignment e.g. _myProperty = value
                             )
                             .WithModifiers(property.GetSetter().Modifiers) // Keep orignial modifiers
                             .WithoutTrivia();
                }

                // Create a new property. Set the type and name
                var newProperty = SyntaxFactory.PropertyDeclaration(property.Type, property.Identifier.ValueText)
                    .WithModifiers(property.Modifiers); // use the modifier(s) of the original property

                // Add getter and setter to accessor list
                var accessors = new List<AccessorDeclarationSyntax>();
                if (getter != null)
                {
                    accessors.Add(getter);
                }
                if (setter != null)
                {
                    accessors.Add(setter);
                }

                // Put together the property with our built up accessors list
                newProperty = newProperty.WithAccessorList(
                        SyntaxFactory.AccessorList(
                            SyntaxFactory.List(accessors)));

                return newProperty; // Returning our new property "replaces" the original
            }

            return base.VisitPropertyDeclaration(property);
        }
    }
}
