using System;
using System.Collections.Immutable;
using System.Linq;
using ExhaustiveMatching;
using FluentAssertions;
using NUnit.Framework;
using PromQL.Parser.Ast;

namespace PromQL.Parser.Tests
{
    [TestFixture]
    public class AstTests
    {
        [Test]
        public void Expr_IsClosed()
        {
            typeof(Expr).Assembly.GetTypes().Where(x => typeof(Expr).IsAssignableFrom(x) && x.IsClass)
                .Should().BeEquivalentTo(
                    typeof(Expr).GetCustomAttributes(typeof(ClosedAttribute), false).Cast<ClosedAttribute>()
                        .Single().Cases
                );
        }
    }
}