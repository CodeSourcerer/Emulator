using System;
using System.Collections.Generic;
using System.Text;
using log4net;
using NESEmulator.Mappers;

namespace NESEmulator
{
    public static class MapperFactory
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Cartridge));

        public static Mapper Create(Cartridge cart)
        {
            Mapper mapper;

            switch (cart.MapperID)
            {
                case 0:
                    Log.Info("Using mapper 000");
                    mapper = new Mapper_000(cart, cart.nPRGBanks, cart.nCHRBanks);
                    break;
                case 1:
                    Log.Info("Using mapper 001");
                    mapper = new Mapper_001(cart, cart.nPRGBanks, cart.nCHRBanks);
                    break;
                case 2:
                    Log.Info("Using mapper 002");
                    mapper = new Mapper_002(cart, cart.nPRGBanks, cart.nCHRBanks);
                    break;
                case 3:
                    Log.Info("Using mapper 003");
                    mapper = new Mapper_003(cart, cart.nPRGBanks, cart.nCHRBanks);
                    break;
                case 4:
                    Log.Info("Using mapper 004");
                    mapper = new Mapper_004(cart, cart.nPRGBanks, cart.nCHRBanks);
                    break;
                default:
                    throw new NotSupportedException($"MapperID '{cart.MapperID}' is not supported");
            }

            return mapper;
        }
    }
}
