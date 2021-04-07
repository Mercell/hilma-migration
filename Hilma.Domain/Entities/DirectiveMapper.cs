using Hilma.Domain.Data.Read;

namespace Hilma.Domain.Integrations.General
{
    public static class DirectiveMapper
    {
        /// <summary>
        /// Directive 2014/24/EU on public procurement
        /// </summary>
        public const string EuEuratom2018Directive = "32018R1046";

        /// <summary>
        /// Directive (EU, Euratom) N:o 2018/1046
        /// </summary>
        public const string EuPublicProcurements2014Directive = "32014L0024";

        /// <summary>
        /// Directive 2014/25/EU on procurement by entities operating in the water, energy, transport and postal services sectors.
        /// </summary>
        public const string EuUtilitiesProcurements2014Directive = "32014L0025";

        /// <summary>
        /// Directive 2014/23/EU Concession notices
        /// </summary>
        public const string EuConcessionProcurement2014Directive = "32014L0023";

        /// <summary>
        /// Directive 2009/81/EC Defence contracts
        /// </summary>
        public const string EuDefenceProcurements2009Directive = "32009L0081";

        public static string GetDirective(NoticeContract notice, NoticeContract parent)
        {
            switch (notice.Type)
            {
                case NoticeContractType.PriorInformation:
                case NoticeContractType.PriorInformationReduceTimeLimits:
                case NoticeContractType.Contract:
                case NoticeContractType.ContractAward:
                    return notice.Project.Organisation.ContractingAuthorityType == OrganisationContractContractingAuthorityType.MaintypeEu ? EuEuratom2018Directive : EuPublicProcurements2014Directive;
                case NoticeContractType.PeriodicIndicativeUtilities:
                case NoticeContractType.PeriodicIndicativeUtilitiesReduceTimeLimits:
                case NoticeContractType.ContractUtilities:
                case NoticeContractType.ContractAwardUtilities:
                case NoticeContractType.QualificationSystemUtilities:
                case NoticeContractType.SocialUtilities:
                case NoticeContractType.SocialUtilitiesPriorInformation:
                case NoticeContractType.SocialUtilitiesContractAward:
                case NoticeContractType.SocialUtilitiesQualificationSystem:
                    return EuUtilitiesProcurements2014Directive;
                case NoticeContractType.DesignContest:
                case NoticeContractType.DesignContestResults:
                    return notice.Project.ProcurementCategory == ProcurementProjectContractProcurementCategory.Public ? EuPublicProcurements2014Directive : EuUtilitiesProcurements2014Directive;
                case NoticeContractType.SocialPriorInformation:
                case NoticeContractType.SocialContract:
                case NoticeContractType.SocialContractAward:
                    return EuPublicProcurements2014Directive;
                case NoticeContractType.SocialConcessionPriorInformation:
                case NoticeContractType.SocialConcessionAward:
                case NoticeContractType.Concession:
                case NoticeContractType.ConcessionAward:
                    return EuConcessionProcurement2014Directive;
                case NoticeContractType.DefenceSimplifiedContract:
                case NoticeContractType.DefenceConcession:
                case NoticeContractType.DefenceContractConcessionnaire:
                case NoticeContractType.DefencePriorInformation:
                case NoticeContractType.DefenceContract:
                case NoticeContractType.DefenceContractAward:
                case NoticeContractType.DefenceContractSub:
                    return EuDefenceProcurements2009Directive;
                case NoticeContractType.ExAnte:
                    if (notice.Project.ProcurementCategory == ProcurementProjectContractProcurementCategory.Defence)
                    {
                        return EuDefenceProcurements2009Directive;
                    } else if (notice.Project.ProcurementCategory == ProcurementProjectContractProcurementCategory.Utility)
                    {
                        return EuUtilitiesProcurements2014Directive;
                    }
                    else if (notice.Project.ProcurementCategory == ProcurementProjectContractProcurementCategory.Lisence)
                    {
                        return EuConcessionProcurement2014Directive;
                    }
                    else if (notice.Project.ProcurementCategory == ProcurementProjectContractProcurementCategory.Public)
                    {
                        return EuPublicProcurements2014Directive;
                    }
                    return null;
                case NoticeContractType.Modification:
                    if (parent != null && notice.ParentId != null)
                    {
                        if( !string.IsNullOrEmpty(parent.LegalBasis))
                        {
                            return parent.LegalBasis;
                        }
                        return GetDirectiveByProcurementCategory(parent);
                    }
                    else
                    {
                        return GetDirectiveByProcurementCategory(notice);
                    }
                case NoticeContractType.BuyerProfile:   // Killed with holy fire
                case NoticeContractType.DpsAward:
                    // Copied from Contract award and contract award utilities based on procurement category
                    if (notice.Project.ProcurementCategory == ProcurementProjectContractProcurementCategory.Public)
                    {
                        return notice.Project.Organisation.ContractingAuthorityType == OrganisationContractContractingAuthorityType.MaintypeEu
                            ? EuEuratom2018Directive
                            : EuPublicProcurements2014Directive;
                    }
                    else { 
                        return EuUtilitiesProcurements2014Directive;
                    }
                default:
                    return null;
            }
        }

        public static string GetDirectiveByProcurementCategory(NoticeContract parent)
        {
            switch (parent.Project.ProcurementCategory)
            {
                case ProcurementProjectContractProcurementCategory.Defence:
                    return EuDefenceProcurements2009Directive;
                case ProcurementProjectContractProcurementCategory.Lisence:
                    return EuConcessionProcurement2014Directive;
                case ProcurementProjectContractProcurementCategory.Public:
                    return EuPublicProcurements2014Directive;
                case ProcurementProjectContractProcurementCategory.Utility:
                    return EuUtilitiesProcurements2014Directive;
                default:
                    return EuPublicProcurements2014Directive;
            }
        }
    }
}
