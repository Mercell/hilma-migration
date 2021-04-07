using Hilma.Domain.Attributes;
using Hilma.Domain.Data.Read;

namespace Hilma.Domain.DataContracts
{
    /// <summary>
    /// Extension class for notice types
    /// </summary>
    [Configuration]
    public class NoticeContractTypes
    {
        /// <summary>
        /// The order is important and shown in this order in UI.
        /// </summary>
        public NoticeContractType[] SupportNoticeContractTypes { get; } =
        {
            NoticeContractType.PriorInformation,
            NoticeContractType.PriorInformationReduceTimeLimits,
            NoticeContractType.Contract,
            NoticeContractType.ContractAward,

            NoticeContractType.PeriodicIndicativeUtilities,
            NoticeContractType.PeriodicIndicativeUtilitiesReduceTimeLimits,
            NoticeContractType.ContractUtilities,
            NoticeContractType.ContractAwardUtilities,

            //NoticeContractType.DesignContest,
            //NoticeContractType.DesignContestResults,

            NoticeContractType.ExAnte,

            NoticeContractType.DefencePriorInformation,
            NoticeContractType.DefenceContract,
            NoticeContractType.DefenceContractAward,

            NoticeContractType.Modification,

            NoticeContractType.SocialPriorInformation,
            NoticeContractType.SocialContract,
            NoticeContractType.SocialContractAward,

            NoticeContractType.SocialUtilitiesPriorInformation,
            NoticeContractType.SocialUtilitiesQualificationSystem,
            NoticeContractType.SocialUtilities,
            NoticeContractType.SocialUtilitiesContractAward,

            NoticeContractType.SocialConcessionPriorInformation,
            NoticeContractType.SocialConcessionAward,

            NoticeContractType.Concession,
            NoticeContractType.ConcessionAward,

            // Nationals below
            NoticeContractType.NationalPriorInformation,
            NoticeContractType.NationalContract, 
            NoticeContractType.NationalSmallValueProcurement,
            NoticeContractType.NationalSmallValueProcurementSocial,

            NoticeContractType.NationalDirectAward,
            NoticeContractType.NationalDesignContest,

            NoticeContractType.NationalDefencePriorInformation,
            NoticeContractType.NationalDefenceContract,

            NoticeContractType.NationalAgricultureContract,
            NoticeContractType.NationalTransparency
        };

        public NoticeContractType[] PublicNotices { get; } = new[] {
            NoticeContractType.PriorInformation,
            NoticeContractType.Contract,
            NoticeContractType.ContractAward,
            NoticeContractType.Modification,
            NoticeContractType.SocialPriorInformation
        };

        /// <summary>
        /// National notices
        /// </summary>
        public NoticeContractType[] NationalNotices { get; } = new[] {
            NoticeContractType.NationalPriorInformation,
            NoticeContractType.NationalContract,
            NoticeContractType.NationalSmallValueProcurement,
            NoticeContractType.NationalSmallValueProcurementSocial,
            NoticeContractType.NationalDesignContest,
            NoticeContractType.NationalDirectAward,
            NoticeContractType.NationalDefencePriorInformation,
            NoticeContractType.NationalDefenceContract,
            NoticeContractType.NationalAgricultureContract,
            NoticeContractType.NationalTransparency
        };

        /// <summary>
        /// Prior information notice types
        /// </summary>
        public NoticeContractType[] PriorInformationNotices { get; } = new[] {
            NoticeContractType.PriorInformation,
            NoticeContractType.PriorInformationReduceTimeLimits,
            NoticeContractType.PeriodicIndicativeUtilities,
            NoticeContractType.PeriodicIndicativeUtilitiesReduceTimeLimits,
            NoticeContractType.DefencePriorInformation,
            NoticeContractType.SocialPriorInformation,
            NoticeContractType.SocialUtilitiesPriorInformation,
            NoticeContractType.NationalPriorInformation
        };

        /// <summary>
        /// Contract notice types
        /// </summary>
        public NoticeContractType[] ContractNotices { get; } = new[] {
            NoticeContractType.Contract,
            NoticeContractType.ContractUtilities,
            NoticeContractType.DefenceContract,
            NoticeContractType.SocialContract,
            NoticeContractType.SocialUtilities,
            NoticeContractType.NationalContract,
            NoticeContractType.NationalSmallValueProcurement,
            NoticeContractType.NationalSmallValueProcurementSocial,
            NoticeContractType.NationalAgricultureContract,
            NoticeContractType.SocialUtilitiesQualificationSystem,
            NoticeContractType.Concession
        };

        /// <summary>
        /// Contract Award notice types
        /// </summary>
        public NoticeContractType[] ContractAwardNotices { get; } = new[] {
            NoticeContractType.ContractAward,
            NoticeContractType.ContractAwardUtilities,
            NoticeContractType.SocialContractAward,
            NoticeContractType.DefenceContractAward,
            NoticeContractType.ConcessionAward,
            NoticeContractType.SocialUtilitiesContractAward,
            NoticeContractType.DpsAward,
            NoticeContractType.SocialConcessionAward,
            NoticeContractType.NationalDirectAward
        };

        /// <summary>
        /// Utilities notice types
        /// </summary>
        public NoticeContractType[] UtilitiesNotices { get; } = new[] {
            NoticeContractType.PeriodicIndicativeUtilities,
            NoticeContractType.PeriodicIndicativeUtilitiesReduceTimeLimits,
            NoticeContractType.ContractAwardUtilities,
            NoticeContractType.ContractUtilities,
            NoticeContractType.QualificationSystemUtilities,
            NoticeContractType.SocialUtilities,
            NoticeContractType.SocialUtilitiesPriorInformation,
            NoticeContractType.SocialUtilitiesContractAward,
            NoticeContractType.SocialUtilitiesQualificationSystem
        };

        /// <summary>
        /// Social notice types
        /// </summary>
        public NoticeContractType[] SocialNotices { get; } = new[] {
            NoticeContractType.SocialContract,
            NoticeContractType.SocialUtilities,
            NoticeContractType.SocialPriorInformation,
            NoticeContractType.SocialContractAward,
            NoticeContractType.SocialConcessionPriorInformation,
            NoticeContractType.SocialConcessionAward,
            NoticeContractType.SocialUtilitiesPriorInformation,
            NoticeContractType.SocialUtilitiesContractAward,
            NoticeContractType.SocialUtilitiesQualificationSystem,
            NoticeContractType.NationalSmallValueProcurementSocial
        };

        /// <summary>
        /// Defence notices
        /// </summary>
        public NoticeContractType[] DefenceNotices { get; } = new[] {
            NoticeContractType.DefenceConcession,
            NoticeContractType.DefencePriorInformation,
            NoticeContractType.DefenceContract,
            NoticeContractType.DefenceContractAward,
            NoticeContractType.DefenceContractConcessionnaire,
            NoticeContractType.DefenceContractSub,
            NoticeContractType.DefenceSimplifiedContract,
            NoticeContractType.NationalDefencePriorInformation,
            NoticeContractType.NationalDefenceContract
        };

        /// <summary>
        /// 2014/24/EU
        /// </summary>
        public NoticeContractType[] EuPublicCategories { get; } = new[] {
            NoticeContractType.PriorInformation,
            NoticeContractType.PriorInformationReduceTimeLimits,
            NoticeContractType.Contract,
            NoticeContractType.ContractAward,
            NoticeContractType.DesignContest,
            NoticeContractType.DesignContestResults,
            NoticeContractType.ExAnte,
            NoticeContractType.Modification,
            NoticeContractType.SocialPriorInformation,
            NoticeContractType.SocialContract,
            NoticeContractType.SocialContractAward
        };

        public NoticeContractType[] NationalPublicCategories { get; } = new[] {
            NoticeContractType.NationalPriorInformation,
            NoticeContractType.NationalContract,
            NoticeContractType.NationalSmallValueProcurement,
            NoticeContractType.NationalDesignContest
        };

        /// <summary>
        /// 2009/81/EC
        /// </summary>
        public NoticeContractType[] EuDefenceCategories { get; } = new[] {
            NoticeContractType.DefencePriorInformation,
            NoticeContractType.DefenceContract,
            NoticeContractType.DefenceContractAward,
            NoticeContractType.ExAnte
        };

        public NoticeContractType[] NationalDefenceCategories { get; } = new[] {
            NoticeContractType.NationalDefencePriorInformation,
            NoticeContractType.NationalDefenceContract
        };

        /// <summary>
        /// 2014/25/EU
        /// </summary>
        public NoticeContractType[] EuUtilityCategories { get; } = new[] {
            NoticeContractType.PeriodicIndicativeUtilities,
            NoticeContractType.ContractUtilities,
            NoticeContractType.ContractAwardUtilities,
            NoticeContractType.QualificationSystemUtilities,
            NoticeContractType.DesignContest,
            NoticeContractType.DesignContestResults,
            NoticeContractType.ExAnte,
            NoticeContractType.Modification,
            NoticeContractType.PeriodicIndicativeUtilitiesReduceTimeLimits,
            NoticeContractType.SocialUtilities,
            NoticeContractType.SocialUtilitiesPriorInformation,
            NoticeContractType.SocialUtilitiesContractAward,
            NoticeContractType.SocialUtilitiesQualificationSystem
        };

        /// <summary>
        /// 2014/23/EU
        /// </summary>
        public NoticeContractType[] EuLisenceCategories { get; } = new[] {
            NoticeContractType.Concession,
            NoticeContractType.ConcessionAward,
            NoticeContractType.SocialConcessionPriorInformation,
            NoticeContractType.SocialConcessionAward,
            NoticeContractType.Modification,
            NoticeContractType.ExAnte
        };

        public NoticeContractType[] AgricultureCategories { get; } = new[] {
            NoticeContractType.NationalAgricultureContract
        };

    }

}
