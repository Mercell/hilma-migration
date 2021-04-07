using Hilma.Domain.Data.Read;

namespace Hilma.Domain.Integrations.HilmaMigration
{
    public class NoticeTypeParser
    {
        public static NoticeContractType ParseNoticeType(INoticeImportModel editaNotice, out bool isCorrigendum, out bool isCancelled)
        {
            isCorrigendum = false;
            isCancelled = false;
            NoticeContractType noticeType;
            switch (editaNotice.FormNumber)
            {
                case "1":
                    if (editaNotice.NoticeType == "PRI_REDUCING_TIME_LIMITS".ToLower())
                    {
                        noticeType = NoticeContractType.PriorInformationReduceTimeLimits;
                    }
                    else
                    {
                        noticeType = NoticeContractType.PriorInformation;
                    }
                    break;
                case "2":
                    noticeType = NoticeContractType.Contract;
                    break;
                case "3":
                    noticeType = NoticeContractType.ContractAward;
                    break;
                case "4":
                    noticeType = NoticeContractType.PeriodicIndicativeUtilities;
                    break;
                case "5":
                    noticeType = NoticeContractType.ContractUtilities;
                    break;
                case "6":
                    noticeType = NoticeContractType.ContractAwardUtilities;
                    break;
                case "14":
                    noticeType = NoticeContractType.Undefined;
                    isCorrigendum = true;
                    break;
                case "15":
                    noticeType = NoticeContractType.ExAnte;
                    break;
                case "16":
                    noticeType = NoticeContractType.DefencePriorInformation;
                    break;
                case "17":
                    noticeType = NoticeContractType.DefenceContract;
                    break;
                case "18":
                    noticeType = NoticeContractType.DefenceContractAward;
                    break;
                case "20":
                    noticeType = NoticeContractType.Modification;
                    break;
                case "24":
                    noticeType = NoticeContractType.Concession;
                    break;
                case "25":
                    noticeType = NoticeContractType.ConcessionAward;
                    break;
                case "50":
                    noticeType = NoticeContractType.NationalAgricultureContract;
                    break;
                case "51":
                    noticeType = NoticeContractType.NationalAgricultureContract;
                    break;
                case "99":
                    switch (editaNotice.NoticeType)
                    {
                        case "domestic_contract":
                            noticeType = NoticeContractType.NationalContract;
                            break;
                        case "request_for_information":
                            noticeType = NoticeContractType.NationalPriorInformation;
                            break;
                        case "domestic_discontinued_notice":
                        case "procurement_discontinued":
                            noticeType = NoticeContractType.NationalContract;
                            isCancelled = true;
                            break;
                        case "corrigendum_notice":
                            noticeType = NoticeContractType.NationalContract;
                            isCorrigendum = true;
                            break;
                        default:
                            noticeType = NoticeContractType.NationalContract;
                            break;
                    }
                    break;
                case "91":
                    noticeType = NoticeContractType.NationalDefenceContract;
                    break;
                case "92":
                    noticeType = NoticeContractType.NationalTransparency;
                    break;
                case "93":
                    noticeType = NoticeContractType.NationalDirectAward;
                    break;
                case "21":
                    switch (editaNotice.NoticeType)
                    {
                        case "contract":
                            noticeType = NoticeContractType.SocialContract;
                            break;
                        case "award_contract":
                            noticeType = NoticeContractType.SocialContractAward;
                            break;
                        case "pri_only":
                            noticeType = NoticeContractType.SocialPriorInformation;
                            break;
                        default:
                            noticeType = NoticeContractType.Undefined;
                            break;
                    }
                    break;
                case "22":
                    noticeType = NoticeContractType.SocialUtilities;
                    break;
                default:
                    noticeType = NoticeContractType.Undefined;
                    break;
            }
            return noticeType;
        }
    }
}
