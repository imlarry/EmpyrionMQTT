using Eleon.Modding;
using System.Collections.Generic;

namespace ESB.Common
{
    public class ItemStackWithName
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
        public int SlotIdx { get; set; }
        public int Ammo { get; set; }
        public int Decay { get; set; }

        public static List<ItemStackWithName> Load(List<ItemStack> inputItemStacks, Dictionary<int,string> blockNameMap)
        {
            List<ItemStackWithName> contentsWithName = new List<ItemStackWithName>();

            foreach (var itemStack in inputItemStacks)
            {
                if (!blockNameMap.TryGetValue(itemStack.id, out string name))
                {
                    name = "<no mapping>";
                }

                contentsWithName.Add(new ItemStackWithName
                {
                    Id = itemStack.id,
                    Name = name,
                    Count = itemStack.count,
                    SlotIdx = itemStack.slotIdx,
                    Ammo = itemStack.ammo,
                    Decay = itemStack.decay
                });
            }

            return contentsWithName;
        }
    }
}

