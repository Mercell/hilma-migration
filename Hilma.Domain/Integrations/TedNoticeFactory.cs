using System;
using System.Xml.Linq;
using Hilma.Domain.Configuration;
using Hilma.Domain.Data.Read;
using Hilma.Domain.Enums;
using Hilma.Domain.Integrations.Defence;
using Hilma.Domain.Integrations.General;

namespace Hilma.Domain.Integrations
{
    /// <summary>
    /// TED Notice Factory - Generates TED integration XML
    /// </summary>
    public class TedNoticeFactory
    {
        private readonly NoticeContract _notice;
        private readonly NoticeContract _parent;
        private readonly string _tedContactEmail;

        private string _eSenderLogin;
        private ITranslationProvider _translationProvider;

        private string _tedSenderOrganisation;

        /// <summary>
        /// Notice factory constructor.
        /// </summary>
        /// <param name="notice">The notice</param>
        /// <param name="parent">The parent notice</param>
        /// <param name="tedSenderOrganisationName">Sender organisation name of TED eSender</param>
        /// <param name="tedContactEmail">The TED contact email</param>
        /// <param name="eSenderLogin">eSenderLogin</param>
        /// <param name="translationProvider">Remote translations (Loco)</param>
        public TedNoticeFactory(NoticeContract notice, NoticeContract parent, string tedSenderOrganisationName,
            string tedContactEmail, string eSenderLogin, ITranslationProvider translationProvider = null)
        {
            _notice = notice;
            _parent = parent;
            _tedContactEmail = tedContactEmail;
            _tedSenderOrganisation = tedSenderOrganisationName;
            _eSenderLogin = eSenderLogin;
            _translationProvider = translationProvider;
        }

        /// <summary>
        /// Creates the XML document 
        /// </summary>
        /// <returns></returns>
        public XDocument CreateDocument()
        {
            if (IsCorrigendum() && !NoticeTypeExtensions.IsDefence(_notice.Type))
            {
                var f14Factory = new General.F14Factory(_notice, _parent, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                return f14Factory.Form14();
            }

            if (IsCorrigendum() && NoticeTypeExtensions.IsDefence(_notice.Type))
            {
                var f14Factory = new Defence.F14Factory(_notice, _parent, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                return f14Factory.CreateForm();
            }

            switch (_notice.Type)
            {
                case NoticeContractType.PriorInformation:
                case NoticeContractType.PriorInformationReduceTimeLimits:
                    var f01Factory = new F01Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                    return f01Factory.CreateForm();
                case NoticeContractType.Contract:
                    var f02Factory = new F02Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                    return f02Factory.CreateForm();
                case NoticeContractType.Undefined:
                    break;
                case NoticeContractType.ContractAward:
                    var f03Factory = new F03Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                    return f03Factory.CreateForm();
                case NoticeContractType.PeriodicIndicativeUtilities:
                case NoticeContractType.PeriodicIndicativeUtilitiesReduceTimeLimits:
                    var f04Factory = new F04Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                    return f04Factory.CreateForm();
                case NoticeContractType.ContractUtilities:
                    var f05Factory = new F05Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                    return f05Factory.CreateForm();
                case NoticeContractType.ContractAwardUtilities:
                    var f06Factory = new F06Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                    return f06Factory.CreateForm();
                case NoticeContractType.QualificationSystemUtilities:
                    break;
                case NoticeContractType.BuyerProfile:
                    break;
                case NoticeContractType.DefenceSimplifiedContract:
                    break;
                case NoticeContractType.DefenceConcession:
                    break;
                case NoticeContractType.DefenceContractConcessionnaire:
                    break;
                case NoticeContractType.DesignContest:
                    var f12Factory = new F12Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                    return f12Factory.CreateForm();
                case NoticeContractType.DesignContestResults:
                    var f13Factory = new F13Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                    return f13Factory.CreateForm();
                case NoticeContractType.ExAnte:
                    var f15factory = new F15Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                    return f15factory.CreateForm();
                case NoticeContractType.DefencePriorInformation:
                    var f16Factory = new F16Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                    return f16Factory.CreateForm();
                case NoticeContractType.DefenceContract:
                    var f17Factory = new F17Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                    return f17Factory.CreateForm();
                case NoticeContractType.DefenceContractAward:
                    return new F18Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider).CreateForm();
                case NoticeContractType.DefenceContractSub:
                    break;
                case NoticeContractType.Modification:
                    var f20Factory = new F20Factory(_notice, _parent, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail);
                    return f20Factory.CreateForm();
                case NoticeContractType.SocialPriorInformation:
                case NoticeContractType.SocialContract:
                case NoticeContractType.SocialContractAward:
                    var f21Factory = new F21Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                    return f21Factory.CreateForm();
                case NoticeContractType.SocialUtilities:
                case NoticeContractType.SocialUtilitiesPriorInformation:
                case NoticeContractType.SocialUtilitiesContractAward:
                case NoticeContractType.SocialUtilitiesQualificationSystem:
                    var f22Factory = new F22Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                    return f22Factory.CreateForm();
                case NoticeContractType.SocialConcessionPriorInformation:
                case NoticeContractType.SocialConcessionAward:
                    var f23Factory = new F23Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                    return f23Factory.CreateForm();
                case NoticeContractType.Concession:
                    var f24Factory = new F24Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                    return f24Factory.CreateForm();
                case NoticeContractType.ConcessionAward:
                    var f25Factory = new F25Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                    return f25Factory.CreateForm();
                case NoticeContractType.DpsAward:
                    if(_notice.Project.ProcurementCategory == ProcurementProjectContractProcurementCategory.Public)
                    {
                        var dpsAward3 = new F03Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                        return dpsAward3.CreateForm();
                    }
                    else
                    {
                        var dpsAward6 = new F06Factory(_notice, _eSenderLogin, _tedSenderOrganisation, _tedContactEmail, _translationProvider);
                        return dpsAward6.CreateForm();
                    }
                default:
                    throw new ArgumentOutOfRangeException($"Notice type: {_notice.Type} is not supported");
            }

            return null;
        }

        // Corrigendum, if parent has been published
        private bool IsCorrigendum()
        {
            return _notice.IsCorrigendum && _parent != null &&
                (_parent.State == PublishState.Published || _parent.State == PublishState.NotPublic);
        }
    }
}
