using System;
using Artemis.Core;
using static System.DateTimeOffset;

namespace Artemis.Plugins.Nodes.DateTime.Nodes;

[Node("To DateTime", "Converts the input to a datetime.", "Conversion", InputType = typeof(object), OutputType = typeof(System.DateTime))]
public class ConvertToDateTimeNode : Node
{
    #region Constructors

    public ConvertToDateTimeNode()
    {
        Input = CreateInputPin<object>();
        UtcDateTime = CreateOutputPin<System.DateTime>("UTC DateTime");
        LocalDateTime = CreateOutputPin<System.DateTime>("Local DateTime");
    }

    #endregion

    #region Properties & Fields

    public InputPin<object> Input { get; }

    public OutputPin<System.DateTime> UtcDateTime { get; }
    public OutputPin<System.DateTime> LocalDateTime { get; }

    #endregion

    #region Methods

    public override void Evaluate()
    {
        DateTimeOffset result = Input.Value switch
        {
            DateTimeOffset dto => dto,
            System.DateTime dt => new DateTimeOffset(dt),
            _ => TryParse(Input.Value)
        };

        UtcDateTime.Value = result.UtcDateTime;
        LocalDateTime.Value = result.LocalDateTime;
    }

    private static DateTimeOffset TryParse(object? input)
    {
        if (input == null)
            return default;

        DateTimeOffset.TryParse(input?.ToString(), out DateTimeOffset result);
        return result;
    }

    #endregion
}