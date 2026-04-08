using Artemis.Core.Nodes;
using Artemis.Plugins.Nodes.DateTime.Nodes;

namespace Artemis.Plugins.Nodes.DateTime;

public class DateTimeNodesProvider : NodeProvider
{
    public override void Enable()
    {
        RegisterNodeType<ConvertToDateTimeNode>();
        RegisterNodeType<SplitToDateTimePartsNode>();
    }

    public override void Disable()
    {
    }
}