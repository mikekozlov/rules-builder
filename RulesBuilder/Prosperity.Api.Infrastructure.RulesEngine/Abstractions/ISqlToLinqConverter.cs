using System.Linq.Expressions;

namespace Prosperity.Api.Infrastructure.RulesEngine.Abstractions;

public interface ISqlToLinqConverter
{
    Expression<Func<T, bool>> ConvertToExpression<T>(string sqlWhereClause);
}
