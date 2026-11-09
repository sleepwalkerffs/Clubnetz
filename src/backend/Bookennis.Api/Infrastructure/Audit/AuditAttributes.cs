using Bookennis.Shared.Controller.Admin.Audit;

namespace Bookennis.Api.Infrastructure.Audit;

/// <summary>The command is about accounts and access: it is logged as <see cref="AuditEventKind.Security"/>.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SecurityAuditAttribute : Attribute;

/// <summary>The query reads sensitive data (an export): every call is logged as <see cref="AuditEventKind.Read"/>. Other queries are not logged.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AuditedReadAttribute : Attribute;

/// <summary>The command is never logged as a command (it runs on every request, e.g. checking an API key). Its data changes still are.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class NotAuditedAttribute : Attribute;
