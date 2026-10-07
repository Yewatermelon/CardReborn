using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Card.Tests.EditMode")]
[assembly: InternalsVisibleTo("Card.Tests.PlayMode")]
// M6-T1：Card.Bootstrap 是场景组合根（运行时装配 internal 序列化字段），与测试程序集同模式。
[assembly: InternalsVisibleTo("Card.Bootstrap")]
