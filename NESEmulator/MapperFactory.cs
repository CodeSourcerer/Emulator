using System;
using System.Collections.Generic;
using System.Text;
using NESEmulator.Mappers;

namespace NESEmulator
{
    public static class MapperFactory
    {
        public static Mapper Create(Cartridge cart)
        {
            Mapper mapper;

            switch (cart.MapperID)
            {
                case 0:
                    mapper = new Mapper_000(cart, cart.nPRGBanks, cart.nCHRBanks);
                    break;
                case 1:
                    mapper = new Mapper_001(cart, cart.nPRGBanks, cart.nCHRBanks);
                    break;
                case 2:
                    mapper = new Mapper_002(cart, cart.nPRGBanks, cart.nCHRBanks);
                    break;
                case 3:
                    mapper = new Mapper_003(cart, cart.nPRGBanks, cart.nCHRBanks);
                    break;
                case 4:
                    mapper = new Mapper_004(cart, cart.nPRGBanks, cart.nCHRBanks);
                    break;
                default:
                    throw new NotSupportedException($"MapperID '{cart.MapperID}' is not supported");
            }

            return mapper;
        }
    }
}
