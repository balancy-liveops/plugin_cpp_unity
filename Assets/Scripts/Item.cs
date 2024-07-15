using System;

namespace Balancy.Models.SmartObjects
{
    public class Item : BaseModel
    {
        // public readonly LocalizedString Name;

        private int _maxStack;
        public int MaxStack => _maxStack;

        // public readonly ItemType Type;

        public Item(IntPtr p, string unnyId, string templateName) : base(p, unnyId, templateName)
        {
        }

        public override void InitData()
        {
            base.InitData();
            _maxStack = GetIntParam(Pointer, "maxStack");
        }
    }
}