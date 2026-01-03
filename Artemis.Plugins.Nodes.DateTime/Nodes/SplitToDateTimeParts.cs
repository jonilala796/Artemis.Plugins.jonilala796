using Artemis.Core;

namespace Artemis.Plugins.Nodes.DateTime.Nodes;

[Node("To DateTime parts", "Splits the input to the parts of datetime.", "Conversion", InputType = typeof(System.DateTime), OutputType = typeof(Numeric))]
public class SplitToDateTimePartsNode : Node
{
    #region Constructors

    public SplitToDateTimePartsNode()
    {
        Year = CreateOutputPin<Numeric>("Year");
        Month = CreateOutputPin<Numeric>("Month");
        Day = CreateOutputPin<Numeric>("Day");
        Hour = CreateOutputPin<Numeric>("Hour");
        Minute = CreateOutputPin<Numeric>("Minute");
        Second = CreateOutputPin<Numeric>("Second");
        Millisecond = CreateOutputPin<Numeric>("Millisecond");
        Microsecond = CreateOutputPin<Numeric>("Microsecond");
        Nanosecond = CreateOutputPin<Numeric>("Nanosecond");
        Ticks = CreateOutputPin<Numeric>("Ticks");
        DayOfYear = CreateOutputPin<Numeric>("Day of Year");
        DayOfWeek = CreateOutputPin<string>("Day of Week");
        Input = CreateInputPin<System.DateTime>();
    }

    #endregion

    #region Properties & Fields

    public InputPin<System.DateTime> Input { get; }
    public OutputPin<Numeric> Year { get; }
    public OutputPin<Numeric> Month { get; }
    public OutputPin<Numeric> Day { get; }
    public OutputPin<Numeric> Hour { get; }
    public OutputPin<Numeric> Minute { get; }
    public OutputPin<Numeric> Second { get; }
    public OutputPin<Numeric> Millisecond { get; }
    public OutputPin<Numeric> Microsecond { get; }
    public OutputPin<Numeric> Nanosecond { get; }
    public OutputPin<Numeric> Ticks { get; }
    public OutputPin<Numeric> DayOfYear { get; }
    public OutputPin<string> DayOfWeek { get; }

    #endregion

    #region Methods

    public override void Evaluate()
    {
        System.DateTime dt = Input.Value;
        Year.Value = dt.Year;
        Month.Value = dt.Month;
        Day.Value = dt.Day;
        Hour.Value = dt.Hour;
        Minute.Value = dt.Minute;
        Second.Value = dt.Second;
        Millisecond.Value = dt.Millisecond;
        Microsecond.Value = dt.Microsecond;
        Nanosecond.Value = dt.Nanosecond;
        Ticks.Value = dt.Ticks;
        DayOfYear.Value = dt.DayOfYear;
        DayOfWeek.Value = dt.DayOfWeek.ToString();
    }

    #endregion
}