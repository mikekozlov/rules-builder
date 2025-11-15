using System.Linq.Expressions;

namespace Prosperity.Api.Infrastructure.RulesEngine;

public interface ISqlToLinqConverter
{
    Expression<Func<T, bool>> ConvertToExpression<T>(string sqlWhereClause);
}
