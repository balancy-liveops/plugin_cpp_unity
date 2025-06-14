namespace Balancy.Models.SmartObjects
{
    public interface IViewModel
    {
        UnnyObject Icon { get; }
        int UnnyPriority { get; }
        UnnyObject UnnyView { get; }
        Balancy.Models.SmartObjects.ViewPlacement UnnyPlacement { get; }
    }
}
