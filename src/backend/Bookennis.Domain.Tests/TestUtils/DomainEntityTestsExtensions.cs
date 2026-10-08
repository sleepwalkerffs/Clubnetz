using System.Linq.Expressions;
using System.Reflection;
using Bookennis.Domain.Base;

namespace Bookennis.Domain.Tests.TestUtils;

public static class DomainEntityTestsExtensions
{
    public static void SetId<T>(this T entity, int id)
        where T : class, IEntity
        => entity.SetPrivateProperty(de => de.Id, id);

    public static void SetPrivateProperty<TObject, TValue>(this TObject o, Expression<Func<TObject, TValue>> propertySelector, TValue? value)
        => UpdatePrivateProperty(o, propertySelector, _ => value);

    public static void UpdatePrivateProperty<TObject, TValue>(this TObject o, Expression<Func<TObject, TValue>> propertySelector, Func<TValue, TValue?> valueModifier)
    {
        var objectType = typeof(TObject);
        var propertyType = typeof(TValue);

        if (propertySelector.Body is not MemberExpression memberExpression
         || memberExpression.NodeType is not ExpressionType.MemberAccess
         || memberExpression.Member.MemberType is not MemberTypes.Property
         || memberExpression.Expression?.NodeType is not ExpressionType.Parameter)
        {
            throw new ArgumentException($"The expression doesn't specify a property on the root object. Example: 'x => x.Id', Actual: '{propertySelector}'", nameof(propertySelector));
        }

        var members = objectType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        // It is required to access the declaring type if the property is declared on a base type,
        // otherwise the GetSetMethod property will return null.
        var property = members.OfType<PropertyInfo>()
                              .FirstOrDefault(pi => pi.Name == memberExpression.Member.Name)!
                              .DeclaringType!
                              .GetProperty(memberExpression.Member.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;

        var value = valueModifier.Invoke((TValue)property.GetGetMethod(true)!.Invoke(o, null)!);

        if (property.GetSetMethod(true) is { } setMethod)
        {
            setMethod.Invoke(o, [value]);
        }
        else
        {
            members.OfType<FieldInfo>()
                   .Single(fi => fi.FieldType.IsAssignableTo(propertyType)
                              && (fi.Name.StartsWith($"<{memberExpression.Member.Name}>")
                               || fi.Name.Equals(memberExpression.Member.Name, StringComparison.InvariantCultureIgnoreCase)))
                   .SetValue(o, value);
        }
    }
}