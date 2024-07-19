using System;

namespace Balancy.Models.SmartObjects
{
    public class Item : BaseModel
    {
        // public readonly LocalizedString Name;

        private int _maxStack;
        public int MaxStack => _maxStack;

        // public readonly ItemType Type;

        public override void InitData()
        {
            base.InitData();
            _maxStack = GetIntParam("maxStack");
        }
    }
}