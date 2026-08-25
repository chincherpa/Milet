using Milet.Domain.Entities.Verkauf;

namespace Milet.Domain.Entities.Lager;

public class BelegPositionSeriennummer
{
    public int Id { get; set; }
    public int BelegPositionId { get; set; }
    public BelegPosition? BelegPosition { get; set; }
    public int SeriennummerId { get; set; }
    public Seriennummer? Seriennummer { get; set; }
}
