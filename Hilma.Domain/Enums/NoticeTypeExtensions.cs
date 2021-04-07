using System.Linq;
using Hilma.Domain.DataContracts;
using Hilma.Domain.Data.Read;

namespace Hilma.Domain.Enums
{
    public static class NoticeTypeExtensions
    {
        private static readonly NoticeContractTypes _types;

        static NoticeTypeExtensions()
        {
            _types = new NoticeContractTypes();
        }

        public static bool IsContract(this NoticeContractType? type)
        {
            return _types.ContractNotices.Contains((NoticeContractType)type);
        }
        public static bool IsPriorInformation(this NoticeContractType? type)
        {
            return _types.PriorInformationNotices.Contains((NoticeContractType)type);
        }

        public static bool IsContractAward(this NoticeContractType? type)
        {
            return _types.ContractAwardNotices.Contains((NoticeContractType)type);
        }

        public static bool IsDefence(this NoticeContractType? type)
        {
            return _types.DefenceNotices.Contains((NoticeContractType)type);
        }

        public static bool IsSocial(this NoticeContractType? type)
        {
            return _types.SocialNotices.Contains((NoticeContractType)type);
        }

        public static bool IsUtilities(this NoticeContractType? type)
        {
            return _types.UtilitiesNotices.Contains((NoticeContractType)type);
        }

        public static bool IsNational(this NoticeContractType? type)
        {
            return _types.NationalNotices.Contains((NoticeContractType)type);
        }
    }


}
