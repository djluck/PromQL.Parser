using System;
using FluentAssertions;
using NUnit.Framework;

namespace PromQL.Parser.Tests
{
    [TestFixture]
    public class TypeCheckerTests
    {
        [Test]
        public void Single_Expected_Type_Not_Found()
        {
            Assert.Throws<TypeChecker.InvalidTypeException>(() => Parser.ParseExpression("sum_over_time(instant_vector)").CheckType())
                .Message.Should().Be("Unexpected type 'instant vector' was provided, expected range vector: 14 (line 1, column 15)");
        }
        
        [Test]
        public void Multiple_Expected_Types_Not_Found()
        {
            Assert.Throws<TypeChecker.InvalidTypeException>(() => Parser.ParseExpression("+'hello'").CheckType())
                .Message.Should().Be("Unexpected type 'string' was provided, expected one of 'scalar', 'instant vector': 1 (line 1, column 2)");
        }
        
        [Test]
        [TestCase("sum_over_time(instant_vector)")]
        [TestCase("hour(range_vector[1h])")]
        public void FunctionCall_InvalidArgumentTypes(string expr)
        {
            Assert.Throws<TypeChecker.InvalidTypeException>(() => Parser.ParseExpression(expr).CheckType());
        }
        
        [Test]
        [TestCase("sum_over_time(range_vector[1d])", ValueType.Vector)]
        [TestCase("hour()", ValueType.Vector)]
        [TestCase("hour(instant_vector)", ValueType.Vector)]
        [TestCase("hour(instant_vector, instant_vector2, instant_vector3)", ValueType.Vector)]
        public void FunctionCall_ValidArgumentTypes(string expr, ValueType expected)
        {
            Parser.ParseExpression(expr).CheckType()
                .Should().Be(expected);
        }

        [Test]
        [TestCase("-'a'")]
        [TestCase("-some_matrix[1d]")]
        public void UnaryExpr_InvalidArgumentTypes(string expr)
        {
            Assert.Throws<TypeChecker.InvalidTypeException>(() => Parser.ParseExpression(expr).CheckType());
        }
        
        [Test]
        [TestCase("-1", ValueType.Scalar)]
        [TestCase("-some_vector", ValueType.Vector)]
        public void UnaryExpr_ValidArgumentTypes(string expr, ValueType expected)
        {
            Parser.ParseExpression(expr).CheckType()
                .Should().Be(expected);
        }
        
        [Test]
        [TestCase("some_matrix[1d][1d:1h]")]
        [TestCase("'string'[1d:1h]")]
        [TestCase("1[1d:1h]")]
        [TestCase("time()[1d:1h]")]
        public void SubqueryExpr_InvalidArgumentTypes(string expr)
        {
            Assert.Throws<TypeChecker.InvalidTypeException>(() => Parser.ParseExpression(expr).CheckType());
        }
        
        [Test]
        [TestCase("some_vector[1d:1h]", ValueType.Matrix)]
        [TestCase("hour()[1d:1h]", ValueType.Matrix)]
        public void SubqueryExpr_ValidArgumentTypes(string expr, ValueType expected)
        {
            Parser.ParseExpression(expr).CheckType()
                .Should().Be(expected);
        }
        
        [Test]
        public void BinaryExpr_InvalidLhsString()
        {
            Assert.Throws<TypeChecker.InvalidTypeException>(
                    () => Parser.ParseExpression("'a' + 1 + 1 + 1").CheckType()
                )
                .Message.Should().Contain("Unexpected type 'string' was provided, expected one of 'scalar', 'instant vector'");
        }
        
        [Test]
        public void BinaryExpr_InvalidRhsString()
        {
            Assert.Throws<TypeChecker.InvalidTypeException>(
                    () => Parser.ParseExpression("1 + 1 + 1 + 'a'").CheckType()
                )
                .Message.Should().Contain("Unexpected type 'string' was provided, expected one of 'scalar', 'instant vector'");
        }
        
        [Test]
        public void BinaryExpr_InvalidLhsMatrix()
        {
            Assert.Throws<TypeChecker.InvalidTypeException>(
                    () => Parser.ParseExpression("some_matrix[1d] + 1 + 1 + 1").CheckType()
                )
                .Message.Should().Contain("Unexpected type 'range vector' was provided, expected one of 'scalar', 'instant vector'");
        }
        
        [Test]
        public void BinaryExpr_InvalidRhsMatrix()
        {
            Assert.Throws<TypeChecker.InvalidTypeException>(
                    () => Parser.ParseExpression("1 + 1 + 1 + some_matrix[1d]").CheckType()
                )
                .Message.Should().Contain("Unexpected type 'range vector' was provided, expected one of 'scalar', 'instant vector'");
        }
        
        [Test]
        public void BinaryExpr_Scalar()
        {
            Parser.ParseExpression("1 > bool 1").CheckType().Should().Be(ValueType.Scalar);
        }
        
        [Test]
        public void BinaryExpr_Vector()
        {
            Parser.ParseExpression("first_vector and second_vector").CheckType().Should().Be(ValueType.Vector);
        }
        
        [Test]
        public void BinaryExpr_VectorScalar()
        {
            Parser.ParseExpression("first_vector > 1").CheckType().Should().Be(ValueType.Vector);
        }

        [Test]
        public void BinaryExpr_VectorScalarSet()
        {
            Assert.Throws<TypeChecker.InvalidTypeException>(
                    () => Parser.ParseExpression("first_vector and 1").CheckType()
                )
                .Message.Should().Contain("set operator And not allowed in binary scalar expression");
        }
        
        [Test]
        public void BinaryExpr_ScalarNoBool()
        {
            Assert.Throws<TypeChecker.InvalidTypeException>(
                () => Parser.ParseExpression("1 > 1").CheckType()
            ).Message.Should().Contain("comparisons between scalars must use bool modifier");
        }

        [Test]
        public void BinarExpr_Associativity()
        {
            Parser.ParseExpression("up == 1 unless on (instance) (test)").CheckType()
                .Should().Be(ValueType.Vector);
        }
        
        [Test]
        public void BinarExpr_Precedence()
        {
            Console.WriteLine(Parser.ParseExpression(@"(metric1) > 80 and 100 - (metric2) < 90"));
            Parser.ParseExpression(@"(metric1) > 80 and 100 - (metric2) < 90").CheckType()
                .Should().Be(ValueType.Vector);
        }
        
        [Test]
        public void ParenExpression_Nested()
        {
            Assert.Throws<TypeChecker.InvalidTypeException>(
                    () => Parser.ParseExpression("((((-'a'))))").CheckType()
                )
                .Message.Should().Be("Unexpected type 'string' was provided, expected one of 'scalar', 'instant vector': 5 (line 1, column 6)");
        }
        
        [Test]
        [TestCase("sum('one')", "'string' was provided, expected instant vector")]
        [TestCase("avg(1)", "'scalar' was provided, expected instant vector")]
        [TestCase("max(scalar(1))", "'scalar' was provided, expected instant vector")]
        [TestCase("min(blah[2m])", "'range vector' was provided, expected instant vector")]
        [TestCase("quantile('1', test)", "'string' was provided, expected scalar")]
        [TestCase("quantile(1, test[1m])", "'range vector' was provided, expected instant vector")]
        [TestCase("topk(2, test[1m])", "'range vector' was provided, expected instant vector")]
        [TestCase("bottomk(one, '2')", "'instant vector' was provided, expected scalar")]
        [TestCase("count_values(one, 'one')", "'instant vector' was provided, expected string")]
        public void AggregateExpression_InvalidParameterTypes(string input, string expected)
        {
            Assert.Throws<TypeChecker.InvalidTypeException>(
                    () => Parser.ParseExpression(input).CheckType()
                )
                .Message.Should().Contain(expected);
        }
    }
}