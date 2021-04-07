using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Hilma.Domain.Data.Read;
using Hilma.Domain.Integrations.General;

namespace Hilma.Domain.Integrations.HilmaMigration
{
    public class NoticeXMLParser
    {
        private static readonly List<string> DefenceNoticeNumber = new List<string>() { "16","17","18"};
        private static readonly List<string> NationalNoticeNumber = new List<string>() { "91", "92","93","99" };
        private static readonly XNamespace _nutsSchema = "http://publications.europa.eu/resource/schema/ted/2021/nuts";

        public NoticeContract ParseNotice(INoticeImportModel importedNotice)
        {
            try
            {
                var doc = XDocument.Parse(importedNotice.Notice);
                XElement formSection = doc.Root.Element("FORM_SECTION");
                NoticeContractType noticeType = NoticeTypeParser.ParseNoticeType(importedNotice, out bool isCorrigendum, out bool isCancelled);

                XElement formElement;

                if (isCorrigendum && importedNotice.FormNumber == "14")
                {
                    formElement = formSection?.Element("F14_2014");
                }
                else
                {
                    formElement = ResolveFormElement(noticeType, formSection, importedNotice);
                }

                if (formElement == null)
                {
                    throw new Exception("Form element could not be resolved");
                }

                var noticeNumber = importedNotice.NoticeNumber;

                if (NationalNoticeNumber.Contains(noticeNumber))
                {
                    return ParseNationalNotices(formElement, noticeNumber, importedNotice, noticeType, isCorrigendum, isCancelled);
                }

                if (DefenceNoticeNumber.Contains(noticeNumber))
                {
                    return ParseDefenceNotices(formElement, noticeNumber, importedNotice, noticeType, isCorrigendum);
                }

                var hilmaStatistics = new HilmaStatistics();
                hilmaStatistics.EnergyEfficiencyConsidered = formSection.Attribute("TAKING_ACCOUNT_ENERGY_EFFICIENCY")?.Value == "YES";
                hilmaStatistics.InnovationConsidered = formSection.Attribute("TAKING_ACCOUNT_INNOVATION")?.Value == "YES";
                hilmaStatistics.SmeParticipationConsidered = formSection.Attribute("TAKING_ACCOUNT_SME_PARTICIPATION")?.Value == "YES";

                return ParseEuNotice(importedNotice, noticeType, formElement, noticeNumber, isCorrigendum, hilmaStatistics);

            }
            catch
            {
                throw;
            }

        }

        private NoticeContract ParseEuNotice(INoticeImportModel importedNotice, NoticeContractType noticeType, XElement formElement, string noticeNumber, bool isCorrigendum, HilmaStatistics hilmaStatistics)
        {
            if (noticeType == NoticeContractType.Undefined && !isCorrigendum)
            {
                throw new NotImplementedException("NoticeType is not supported. Type was " + formElement.Element("NOTICE")?.Attribute("TYPE")?.Value);
            }

            var objectContract = formElement.Element("OBJECT_CONTRACT");
            var objectDescriptions = formElement.Descendants("OBJECT_DESCR");
            var awardContract = formElement.Descendants("AWARD_CONTRACT");
            var contractingBody = formElement.Element("CONTRACTING_BODY");
            var procedure = formElement.Element("PROCEDURE");
            var complementaryInfo = formElement.Element("COMPLEMENTARY_INFO");
            var reviewBodyAddress = complementaryInfo?.Element("ADDRESS_REVIEW_BODY");
            var lefti = formElement.Element("LEFTI");
            var directive = formElement.Element("LEGAL_BASIS")?.Attribute("VALUE")?.Value;
            var changes = formElement?.Element("CHANGES");

            XElement addressContractinBody = contractingBody.Element("ADDRESS_CONTRACTING_BODY");
            var procedureInformation = ParseProcedureInformation(procedure);
            var lotsInfo = ParseLotsInfo(objectContract);

            var notice = new NoticeContract
            {
                NoticeNumber = noticeNumber,
                CreatorId = null,
                Type = noticeType,
                LegalBasis = directive,
                Project = ParseProject(objectContract, addressContractinBody, contractingBody, noticeType, directive),
                ComplementaryInformation = ParseComplementaryInformation(complementaryInfo),
                DateCreated = importedNotice.HilmaSubmissionDate,
                Language = formElement.Attribute("LG")?.Value ?? "FI",
                ContactPerson = ParseContactPerson(addressContractinBody),
                ProcurementObject = ParseProcurementObject(objectContract),
                ObjectDescriptions = ParseObjectDescription(_nutsSchema, objectDescriptions, awardContract, lotsInfo.DivisionLots).ToArray(),
                DatePublished = importedNotice.HilmaPublishedDate,
                ProcedureInformation = procedureInformation,
                AttachmentInformation = new AttachmentInformation()
                {
                    
                    Links = ParseLinksFromAttachments(formElement.Element("ATTACHMENTS"))
                },
                TenderingInformation = ParseTenderingInformation(objectContract, procedure),

                ConditionsInformation = ParseConditionsInformation(lefti),
                CommunicationInformation = ParseCommunicationInformation(_nutsSchema, contractingBody),
                LotsInfo = lotsInfo,
                ProceduresForReview = ParseProceduresForReview(reviewBodyAddress, complementaryInfo),
                IsLatest = true,
                Attachments = new AttachmentViewModel[0],
                TedPublishState = importedNotice.IsPublishedInTed ? NoticeContractTedPublishState.PublishedInTed : NoticeContractTedPublishState.Undefined,
                TedSubmissionId = importedNotice.TedSubmissionId,
                NoticeOjsNumber = importedNotice.NoticeOjsNumber,
                TedPublicationInfo = ParseTedPublicationInfo(importedNotice),
                
                PreviousNoticeOjsNumber = procedure?.Element("NOTICE_NUMBER_OJ")?.Value ?? complementaryInfo?.Element("NOTICE_NUMBER_OJ")?.Value,
                CorrigendumPreviousNoticeNumber = complementaryInfo?.Element("NO_DOC_EXT")?.Value,
                IsCorrigendum = isCorrigendum,
                Annexes = ParseAnnex(procedureInformation?.ProcedureType ?? ProcedureInformationProcedureType.Undefined, procedure, GetProcurementCategoryByDirective(directive)),
                Modifications = ParseEuModifications(formElement?.Element("MODIFICATIONS_CONTRACT"), _nutsSchema),
                TedPublishRequestSentDate = ParseDate(complementaryInfo.Element("DATE_DISPATCH_NOTICE")?.Value).GetValueOrDefault(),
                HilmaStatistics = hilmaStatistics
            };

            if (importedNotice.FormNumber == "14")
            {
                notice.Changes = ParseChanges(changes);
                notice.CorrigendumAdditionalInformation = ParsePElements(changes?.Element("INFO_ADD"));
            }

            return notice;
        }

        private static Modifications ParseEuModifications(XElement modificationsContract, XNamespace nutsSchema)
        {
            if (modificationsContract == null)
            {
                return null;
            }

            var descriptionProcurement = modificationsContract?.Element("DESCRIPTION_PROCUREMENT");

            return new Modifications()
            {
                AdditionalCpvCodes = ParseAdditionalCpvCodes(descriptionProcurement),
                AwardedToGroupOfEconomicOperators = descriptionProcurement?.Element("CONTRACTORS")?.Element("AWARDED_TO_GROUP") != null,
                Contractors = new List<ContractorContactInformation> {
                     ParseContractor(descriptionProcurement?.Element("CONTRACTORS")?.Element("CONTRACTOR")?.Element("ADDRESS_CONTRACTOR"))
                },
                Description = ParsePElements(modificationsContract?.Element("INFO_MODIFICATIONS")?.Element("SHORT_DESCR")),
                DescrProcurement = ParsePElements(descriptionProcurement?.Element("SHORT_DESCR")),
                IncreaseBeforeModifications = new ValueContract()
                {
                    Currency = modificationsContract?.Element("INFO_MODIFICATIONS")?.Element("VALUES")?.Element("VAL_TOTAL_BEFORE")?.Attribute("CURRENCY")?.Value,
                    Value = ParseDouble(modificationsContract?.Element("INFO_MODIFICATIONS")?.Element("VALUES")?.Element("VAL_TOTAL_BEFORE")?.Value)
                },
                IncreaseAfterModifications = new ValueContract()
                {
                    Currency = modificationsContract?.Element("INFO_MODIFICATIONS")?.Element("VALUES")?.Element("VAL_TOTAL_AFTER")?.Attribute("CURRENCY")?.Value,
                    Value = ParseDouble(modificationsContract?.Element("INFO_MODIFICATIONS")?.Element("VALUES")?.Element("VAL_TOTAL_AFTER")?.Value)
                },
                JustificationForDurationOverEightYears = ParsePElements(descriptionProcurement?.Element("JUSTIFICATION")),
                JustificationForDurationOverFourYears = ParsePElements(descriptionProcurement?.Element("JUSTIFICATION")),
                MainCpvCode = ParseCpvCode(descriptionProcurement),
                MainsiteplaceWorksDelivery = ParsePElements(descriptionProcurement?.Element("MAIN_SITE")),
                NutsCodes = ParseNutsCodes(nutsSchema, descriptionProcurement),
                Reason = modificationsContract?.Element("INFO_MODIFICATIONS")?.Element("ADDITIONAL_NEED") != null ? ModificationsReason.ModNeedForAdditional : ModificationsReason.ModNeedByCircums,
                ReasonDescriptionCircumstances = ParsePElements(modificationsContract?.Element("INFO_MODIFICATIONS")?.Element("UNFORESEEN_CIRCUMSTANCE")),
                ReasonDescriptionEconomic = ParsePElements(modificationsContract?.Element("INFO_MODIFICATIONS")?.Element("ADDITIONAL_NEED")),
                TimeFrame = ParseTimeFrame(descriptionProcurement),
                TotalValue = new ValueContract()
                {
                    Currency = descriptionProcurement?.Element("VALUES")?.Element("VAL_TOTAL")?.Attribute("CURRENCY")?.Value,
                    Value = ParseDouble(descriptionProcurement?.Element("VALUES")?.Element("VAL_TOTAL")?.Value)
                },
                
            };
        }

        private NoticeContract ParseNationalNotices(XElement formElement, string noticeNumber, INoticeImportModel importedNotice, NoticeContractType noticeType, bool isCorrigendum, bool isCancelled)
        {

            var domesticContract = formElement.Element("FD_DOMESTIC_CONTRACT") != null ? formElement.Element("FD_DOMESTIC_CONTRACT") :
                                   formElement?.Element("FD_DOMESTIC_TRANSPARENCY_NOTICE") != null ? formElement?.Element("FD_DOMESTIC_TRANSPARENCY_NOTICE") :
                                   formElement.Element("FD_DOMESTIC_DIRECT_AWARD") != null ? formElement.Element("FD_DOMESTIC_DIRECT_AWARD") :
                                   formElement.Element("FD_AGRICULTURE_CONTRACT_AUTHORITY") != null ? formElement.Element("FD_AGRICULTURE_CONTRACT_AUTHORITY") : formElement.Element("FD_AGRICULTURE_CONTRACT_TENDER") != null ? formElement.Element("FD_AGRICULTURE_CONTRACT_TENDER")
                                   : formElement.Element("FD_GENERAL_CONTRACT");
            var domesticNoticeType = domesticContract?.Element("DOMESTIC_NOTICE_TYPE");


            if (noticeType == NoticeContractType.Undefined && !isCorrigendum)
            {
                throw new NotImplementedException("NoticeType is not supported. Type was " + domesticNoticeType?.Elements()?.First()?.Name?.LocalName);
            }

            var domesticAuthorityInformation = domesticContract?.Element("DOMESTIC_AUTHORITY_INFORMATION") ??
                                               domesticContract?.Element("AGRICULTURE_AUTHORITY_INFORMATION_AUTHORITY") ??
                                               domesticContract?.Element("AGRICULTURE_AUTHORITY_INFORMATION_TENDER") ??
                                               domesticContract?.Element("GENERAL_AUTHORITY_INFORMATION");
            var domesticObjectContract = domesticContract?.Element("DOMESTIC_OBJECT_INFORMATION") ??
                                         domesticContract?.Element("AGRICULTURE_OBJECT_INFORMATION_AUTHORITY") ??
                                         domesticContract?.Element("AGRICULTURE_OBJECT_INFORMATION_TENDER") ??
                                         domesticContract?.Element("GENERAL_OBJECT_INFORMATION");
            var domesticContractRC = domesticContract?.Element("DOMESTIC_CONTRACT_RELATING_CONDITIONS") ??
                                     domesticContract?.Element("AGRICULTURE_CONTRACT_RELATING_CONDITIONS_TENDER") ??
                                     domesticContract?.Element("GENERAL_CONTRACT_RELATING_CONDITIONS");
            var domesticNameAddresses = domesticAuthorityInformation?.Element("DOMESTIC_NAME_ADDRESSES") ??
                                        domesticAuthorityInformation?.Element("AGRICULTURE_NAME_ADDRESSES") ??
                                        domesticAuthorityInformation?.Element("GENERAL_NAME_ADDRESSES");
            var organisationInformation = ParseContractingBodyInformation(_nutsSchema, domesticNameAddresses);
            var domesticProcedureDefinition = domesticContract?.Element("DOMESTIC_PROCEDURE_DEFINITION");

            var previousNoticeNumber = importedNotice.PreviousNoticeNumber;
            if (string.IsNullOrEmpty(previousNoticeNumber) && isCancelled)
            {
                previousNoticeNumber = domesticNoticeType?.Element("PROCUREMENT_DISCONTINUED")
                    ?.Element("DOMESTIC_DISCONTINUED_NOTICE")?.Attribute("NO_DOC_EXT")?.Value;
            }
            else if (string.IsNullOrEmpty(previousNoticeNumber) && isCorrigendum)
            {
                previousNoticeNumber = domesticNoticeType?.Element("CORRIGENDUM_NOTICE")
                    ?.Element("DOMESTIC_ORIGINAL_NOTICE")?.Attribute("NO_DOC_EXT")?.Value;
            }

            var notice = new NoticeContract()
            {
                PreviousNoticeOjsNumber = previousNoticeNumber,
                IsCorrigendum = isCorrigendum,
                IsCancelled = isCancelled,
                TedPublishState = importedNotice.IsPublishedInTed ? NoticeContractTedPublishState.PublishedInTed : NoticeContractTedPublishState.Undefined,
                CancelledReason = new[] { domesticContract?.Element("DOMESTIC_DISCONTINUED_JUSTIFICATION")?.Element("ADDITIONAL_INFORMATION")?.Value },
                CreatorId = null,
                NoticeNumber = noticeNumber,
                Language = formElement.Attribute("LG")?.Value ?? "FI",
                Type = noticeType == NoticeContractType.NationalDirectAward ? noticeType :
                       noticeType == NoticeContractType.NationalContract && domesticContract?.Attribute("DOMESTIC_CTYPE")?.Value == "DESIGN_CONTEST" ? NoticeContractType.NationalDesignContest :
                       noticeType == NoticeContractType.NationalContract ? noticeType :
                       noticeType == NoticeContractType.NationalDefenceContract ? noticeType :
                       noticeType == NoticeContractType.NationalTransparency ? noticeType :
                       noticeType == NoticeContractType.NationalAgricultureContract ? noticeType :
                       ParseNationalNoticeType(domesticNoticeType?.Elements()?.First()?.Name?.LocalName),
                ContactPerson = ParseContactPerson(domesticNameAddresses),
                IsLatest = true,
                Project = new ProcurementProjectContract()
                {
                    Title = domesticObjectContract?.Element("TITLE_CONTRACT")?.Value,
                    ReferenceNumber = domesticObjectContract?.Element("FILE_REFERENCE_NUMBER")?.Value,

                    Organisation = new OrganisationContract()
                    {
                        Information = organisationInformation,
                        ContractingAuthorityType = FromTEDFormatContractingAuthorityType(domesticAuthorityInformation?.Element("DOMESTIC_TYPE_OF_CONTRACTING")?.Descendants()?.First()?.Name?.ToString() ??
                        domesticAuthorityInformation?.Element("GENERAL_TYPE_OF_CONTRACTING")?.Descendants()?.First()?.Name?.ToString()),
                        OtherContractingAuthorityType = domesticAuthorityInformation?.Element("DOMESTIC_TYPE_OF_CONTRACTING")?.Element("OTHER")?.Value,

                    },
                    ProcurementCategory = ProcurementProjectContractProcurementCategory.Public,
                    ContractType = ParseContractType(domesticContract?.Attribute("DOMESTIC_CTYPE")?.Value ?? domesticContract?.Attribute("AGRICULTURE_CTYPE")?.Value),
                    CentralPurchasing = domesticAuthorityInformation?.Element("PURCHASING_ON_BEHALF")?.Attribute("VALUE")?.Value == "YES",
                    
                    Publish = ProcurementProjectContractPublish.ToHilma
                },
                CommunicationInformation = ParseNationalCommunicationInformation(domesticAuthorityInformation, organisationInformation),
                ProcurementObject = new ProcurementObject()
                {
                    MainCpvCode = new CpvCode
                    {
                        Code = domesticObjectContract?.Element("CPV")?.Element("CPV_MAIN")?.Element("CPV_CODE")?.Attribute("CODE")?.Value,
                        VocCodes = domesticObjectContract?.Element("CPV")?.Element("CPV_MAIN")?.Elements("CPV_SUPPLEMENTARY_CODE")?.Select(s => new VocCode { Code = s?.Attribute("CODE")?.Value }).ToArray()
                    },
                    EstimatedValue = ParseNationalEstimatedValue(domesticObjectContract),
                    ShortDescription = ParsePElements(domesticObjectContract?.Element("SHORT_DESCRIPTION")),
                    
                },
                ObjectDescriptions = ParseNationalObjectDescriptions(_nutsSchema, domesticContract, domesticContractRC, domesticObjectContract).ToArray(),
                ProcedureInformation = new ProcedureInformation()
                {
                    ContestType = domesticProcedureDefinition?.Element("DOMESTIC_TYPE_OF_PROCEDURE")?.Element("OPEN") != null ? ProcedureInformationContestType.Open :
                                  domesticProcedureDefinition?.Element("DOMESTIC_TYPE_OF_PROCEDURE")?.Element("DESIGN_CONTEST") != null ? ProcedureInformationContestType.TypeRestricted : ProcedureInformationContestType.Undefined,
                    
                    ProcedureType = ParseNationalProcedureInformationProcedureTypes(domesticProcedureDefinition?.Element("DOMESTIC_TYPE_OF_PROCEDURE")),
                    FrameworkAgreement = new FrameworkAgreementInformation()
                    {
                        IncludesFrameworkAgreement = domesticProcedureDefinition?.Element("FRAMEWORK_AGREEMENT_IS_ESTABLISH")?.Attribute("VALUE")?.Value == "YES" ? true : false,
                    },
                    National = new ProcedureInformationNational()
                    {
                        OtherProcedure = ParsePElements(domesticProcedureDefinition?.Element("DOMESTIC_PROCEDURE_DEFINITION")),
                        AdditionalProcedureInformation = ParsePElements(domesticContractRC?.Element("SELECTION_CRITERIA_INFORMATION") ?? domesticProcedureDefinition?.Element("ADDITIONAL_INFORMATION")),
                        LimitedNumberOfParticipants = (domesticProcedureDefinition?.Element("LIMITED_NUMBER_OF_CANDIDATES") != null && domesticProcedureDefinition?.Element("LIMITED_NUMBER_OF_CANDIDATES")?.Attribute("VALUE")?.Value == "YES"),
                        NumberOfParticipants = ParseInt(domesticProcedureDefinition?.Element("NUMBER_OF_CANDIDATES")?.Value),

                        SelectionCriteria = ParsePElements(domesticProcedureDefinition?.Element("PROCEDURE_DESCRIPTION")),

                        TransparencyType = formElement?.Attribute("NOTICE_TYPE")?.Value == "OTHER_MEASURES" ? ProcedureInformationNationalTransparencyType.TransparencyOther :
                        formElement?.Attribute("NOTICE_TYPE")?.Value == "IN_HOUSE_DIRECT_ECONOMIC_ACTIVITY" ? ProcedureInformationNationalTransparencyType.TransparencyLaw15 :
                        formElement?.Attribute("NOTICE_TYPE")?.Value == "CO_OPERATING_ECONOMIC_ACTIVITY" ? ProcedureInformationNationalTransparencyType.TransparencyLaw16 : ProcedureInformationNationalTransparencyType.Undefined

                    }
                },
                ConditionsInformationNational = new ConditionsInformationNational()
                {
                    ParticipantSuitabilityCriteria = ParsePElements(domesticContractRC?.Element("SELECTION_CRITERIA") ?? domesticContractRC?.Element("GENERAL_AWARD_CRITERIAS")),
                    RequiredCertifications = ParsePElements(domesticContractRC?.Element("SELECTION_CRITERIA_CERTIFICATIONS")),
                    AdditionalInformation = ParsePElements(domesticContractRC?.Element("SELECTION_CRITERIA_ADDITIONAL_INFORMATION") ?? domesticContractRC?.Element("GENERAL_ADDITIONAL_CONDITIONS")),
                    ReservedForShelteredWorkshopOrProgram = domesticProcedureDefinition?.Element("RESERVED_CONTRACTS")?.Attribute("VALUE").Value == "YES",
                    
                },
                ConditionsInformation = new ConditionsInformation(),
                TenderingInformation = new TenderingInformation()
                {
                    TendersOrRequestsToParticipateDueDateTime = ParseDateTimeFromElements(domesticContractRC?.Element("RECEIPT_LIMIT_DATE") != null ? domesticContractRC?.Element("RECEIPT_LIMIT_DATE") :
                                                                domesticProcedureDefinition?.Element("RECEIPT_LIMIT_DATE") ?? domesticAuthorityInformation?.Element("RECEIPT_LIMIT_DATE") ?? domesticContract?.Element("GENERAL_PERIOD_FOR_APPLICATIONS")?.Element("RECEIPT_LIMIT_DATE")),
                    EstimatedExecutionTimeFrame = new TimeFrame()
                    {
                        EndDate = ParseDateTimeFromElements(domesticObjectContract.Element("PERIOD_WORK_DATE_STARTING")?.Element("INTERVAL_DATE")?.Element("END_DATE") ?? domesticAuthorityInformation?.Element("RECEIPT_LIMIT_DATE") ?? domesticObjectContract?.Element("PROCEDURE_DATE_STARTING"))
                        ,
                        BeginDate = ParseDateTimeFromElements(domesticObjectContract.Element("PERIOD_WORK_DATE_STARTING")?.Element("INTERVAL_DATE")?.Element("START_DATE"))
                    },
                    
                },
                ProceduresForReview = ParseProceduresForReview(domesticContract?.Element("DOMESTIC_ADDRESS_REVIEW_BODY"), null),
                ComplementaryInformation = new ComplementaryInformation()
                {
                    AdditionalInformation = domesticContractRC?.Descendants("ADDITIONAL_INFORMATION")?.SelectMany(i => ParsePElements(i)).ToArray() ?? new string[0],
                },
                LotsInfo = new LotsInfo()
                {
                    DivisionLots = domesticProcedureDefinition?.Element("DIVISION_INTO_LOTS") != null ? domesticProcedureDefinition?.Element("DIVISION_INTO_LOTS")?.Attribute("VALUE").Value == "YES" :
                                   domesticObjectContract?.Element("DIVISION_INTO_LOTS")?.Attribute("VALUE").Value == "YES"
                },
                AttachmentInformation = new AttachmentInformation()
                {
                    Links = ParseLinksFromAttachments(domesticContract?.Element("ATTACHMENTS")),
                    Description = ParsePElements(domesticObjectContract?.Element("AGRICULTURE_DESCRIPTION_OF_DOCUMENTS")),
                    
                },
                Attachments = new AttachmentViewModel[0],
                
                DatePublished = importedNotice?.HilmaPublishedDate
            };

            if (notice.Type == NoticeContractType.NationalDirectAward)
            {
                notice.Annexes = new Annex()
                {
                    DirectNational = new AnnexNational()
                    {
                        Justification = ParsePElements(domesticProcedureDefinition?.Element("DOMESTIC_TYPE_OF_PROCEDURE")?.Element("PT_DIRECT_AWARD")?.Element("JUSTIFICATION_DIRECT_AWARD")),
                        PurchaseType = AnnexNationalPurchaseType.JustifiableDirectPurchase
                    }
                };
            }

            if (notice.Project.Organisation.ContractingAuthorityType == OrganisationContractContractingAuthorityType.OtherType)
            {
                if (string.IsNullOrEmpty(notice.Project.Organisation.OtherContractingAuthorityType))
                {
                    notice.Project.Organisation.OtherContractingAuthorityType = "Ei määritelty";
                }
            }

            notice.Project.Organisation.Information.NutsCodes = ParseSingleNutsCode(domesticContract?.Element("DOMESTIC_OBJECT_INFORMATION")?.Element(_nutsSchema + "NUTS"));

            return notice;
        }

        private string[] ParseSingleNutsCode(XElement nutsElement)
        {
            var nutsCode = nutsElement
                ?.Attribute("CODE")?.Value;
            return string.IsNullOrEmpty(nutsCode) ? new[] { nutsCode } : new string[0];
        }

        private Link[] ParseLinksFromAttachments(XElement attachments)
        {
            if (attachments == null)
            {
                return new Link[0];
            }

            var hilmaUrl = "https://vanha.hankintailmoitukset.fi";
            var urls = new List<string>() { };

            urls.AddRange(attachments?.Elements("ATTACHMENT")
                .Where(a => a.Attribute("TYPE")?.Value == "INTERNAL")
                .Select(a => $"{hilmaUrl}{a.Value}"));

            urls.AddRange(attachments?.Elements("ATTACHMENT")
               .Where(a => a.Attribute("TYPE")?.Value == "EXTERNAL")
               .Select(a => $"{a.Value}"));

            return urls.Select(url => new Link { Url = url }).ToArray();

        }


        private NoticeContract ParseDefenceNotices(XElement formElement, string noticeNumber, INoticeImportModel importedNotice, NoticeContractType noticeType, bool isCorrigendum)
        {
            var prefix = noticeType == NoticeContractType.DefenceContract ? "_CONTRACT" : "_CONTRACT_AWARD";
            var prefix1 = noticeType == NoticeContractType.DefenceContractAward ? "_AWARD" : "";
            var prefix2 = noticeType == NoticeContractType.DefenceContractAward ? "_CONTRACT_AWARD" : "";
            var prefix3 = noticeType == NoticeContractType.DefenceContractAward ? "_CONTRACT_AWARD_NOTICE" : "";
            var prefix4 = noticeType == NoticeContractType.DefenceContractAward ? "AWARD" : "NOTICE";
            var prefix5 = noticeType == NoticeContractType.DefenceContractAward ? "AWARD_NOTICE" : "CONTRACT";
            var prefix6 = noticeType == NoticeContractType.DefenceContractAward ? "W_PUB_" : "";
            var prefix7 = noticeType == NoticeContractType.DefenceContractAward ? "_PUB_" : "_";
            var prefix8 = noticeType == NoticeContractType.DefenceContractAward ? "F18_" : "";
            var prefix9 = noticeType == NoticeContractType.DefenceContractAward ? "_DEFENCE" : "";
            var contracting = "CONTRACTING_";
            var prior = "";

            if (noticeType == NoticeContractType.DefencePriorInformation)
            {
                prefix = "_PRIOR_INFORMATION";
                contracting = "";
                prior = "PRIOR_";
            }


            var defenceContract = formElement.Element($"FD{prefix}_DEFENCE");
            var defenceAuthorityInformation = defenceContract?.Element($"{contracting}AUTHORITY_{prior}INFORMATION{prefix2}_DEFENCE");
            var defenceNameAdress = defenceAuthorityInformation?.Element($"NAME_ADDRESSES_CONTACT_CONTRACT{prefix1}") ?? defenceAuthorityInformation?.Element("NAME_ADDRESSES_CONTACT_PRIOR_INFORMATION");
            var concessionaireProfile = defenceNameAdress?.Element("CA_CE_CONCESSIONAIRE_PROFILE");

            var defenceContractInformation = defenceContract?.Element($"OBJECT_CONTRACT_INFORMATION{prefix3}_DEFENCE") ?? defenceContract?.Element("OBJECT_WORKS_SUPPLIES_SERVICES_PRIOR_INFORMATION");

            var othInfoPriorInformation = defenceContract?.Element("OTH_INFO_PRIOR_INFORMATION");

            var defenceLeftiContract = defenceContract?.Element("LEFTI_CONTRACT_DEFENCE") ?? defenceContract;
            var defenceProcedureDefinitonContract = defenceContract?.Element($"PROCEDURE_DEFINITION_CONTRACT{prefix1}_NOTICE_DEFENCE");
            var defenceCcomplimentaryInformation = defenceContract?.Element($"COMPLEMENTARY_INFORMATION_CONTRACT_{prefix4}");
            var reviewBody = defenceCcomplimentaryInformation?.Element("PROCEDURES_FOR_APPEAL")?.Element("APPEAL_PROCEDURE_BODY_RESPONSIBLE")?.Element("CONTACT_DATA_WITHOUT_RESPONSIBLE_NAME");
            var administrativeInformationContractNoticeDefence = defenceProcedureDefinitonContract?.Element($"ADMINISTRATIVE_INFORMATION_CONTRACT_{prefix4}_DEFENCE");

            var organisationInformation = ParseContractingBodyInformation(_nutsSchema, concessionaireProfile);
            var ContractRC = defenceLeftiContract?.Element("CONTRACT_RELATING_CONDITIONS");

            var descriptionContractInformation = defenceContractInformation.Element($"DESCRIPTION_{prefix5}_INFORMATION_DEFENCE") ?? defenceContractInformation;

            var typeContractDefence = descriptionContractInformation?.Element($"TYPE_CONTRACT_{prefix6}DEFENCE");
            var typeAndActivities = defenceAuthorityInformation?.Element("TYPE_AND_ACTIVITIES_OR_CONTRACTING_ENTITY_AND_PURCHASING_ON_BEHALF")?.Element("TYPE_AND_ACTIVITIES");
            var typeOfProcedure = defenceProcedureDefinitonContract?.Element($"TYPE_OF_PROCEDURE{prefix2}_DEFENCE");
            var awardOfContractDefence = defenceContract?.Element("AWARD_OF_CONTRACT_DEFENCE");
            var previousPublicationExist = administrativeInformationContractNoticeDefence?.Element("PREVIOUS_PUBLICATION_INFORMATION_NOTICE_F18")?.Element("PREVIOUS_PUBLICATION_EXISTS_F18");


            var notice = new NoticeContract()
            {
                PreviousNoticeOjsNumber = null,
                TedSubmissionId = importedNotice.TedSubmissionId,
                TedPublishState = importedNotice.IsPublishedInTed ? NoticeContractTedPublishState.PublishedInTed : NoticeContractTedPublishState.Undefined,
                NoticeOjsNumber = importedNotice.NoticeOjsNumber,
                IsCorrigendum = isCorrigendum,
                CreatorId = null,
                NoticeNumber = noticeNumber,
                Language = formElement.Attribute("LG")?.Value ?? "FI",
                Type = noticeType,
                ContactPerson = ParseContactPerson(concessionaireProfile),
                IsLatest = true,
                Project = new ProcurementProjectContract()
                {
                    Title = descriptionContractInformation?.Element("TITLE_CONTRACT")?.Value,
                    ReferenceNumber = administrativeInformationContractNoticeDefence?.Element("FILE_REFERENCE_NUMBER")?.Element("P")?.Value,

                    Organisation = new OrganisationContract()
                    {
                        Information = organisationInformation,
                        ContractingAuthorityType = FromTEDFormatContractingAuthorityType(typeAndActivities?.Element("TYPE_OF_CONTRACTING_AUTHORITY")?.Attribute("VALUE")?.Value != null ?
                        typeAndActivities?.Element("TYPE_OF_CONTRACTING_AUTHORITY")?.Attribute("VALUE")?.Value : typeAndActivities?.Element("TYPE_OF_CONTRACTING_AUTHORITY_OTHER")?.Attribute("VALUE")?.Value),

                        OtherContractingAuthorityType = defenceAuthorityInformation?.Element("TYPE_OF_CONTRACTING_AUTHORITY")?.Element("OTHER")?.Value != null ?
                                                        defenceAuthorityInformation?.Element("TYPE_OF_CONTRACTING_AUTHORITY")?.Element("OTHER")?.Value :
                                                        typeAndActivities?.Element("TYPE_OF_CONTRACTING_AUTHORITY_OTHER")?.Value,

                        MainActivity = FromTEDFormatMainActivity(typeAndActivities?.Element("TYPE_OF_ACTIVITY") != null ? typeAndActivities?.Element("TYPE_OF_ACTIVITY")?.Attribute("VALUE")?.Value : typeAndActivities?.Element("TYPE_OF_ACTIVITY_OTHER")?.Attribute("VALUE")?.Value),

                        OtherMainActivity = typeAndActivities?.Element("TYPE_OF_ACTIVITY_OTHER")?.Value,

                        
                    },
                    ProcurementCategory = ProcurementProjectContractProcurementCategory.Defence,

                    ContractType = ParseContractType(typeContractDefence?.Element("TYPE_CONTRACT")?.Attribute("VALUE")?.Value ?? defenceContractInformation?.Element("TYPE_CONTRACT_PLACE_DELIVERY_DEFENCE")?.Element("TYPE_CONTRACT_PI_DEFENCE")?.Element("TYPE_CONTRACT")?.Attribute("VALUE")?.Value),

                    CentralPurchasing = defenceAuthorityInformation?.Element("PURCHASING_ON_BEHALF")?.Element("PURCHASING_ON_BEHALF_YES") != null,
                    DefenceCategory = new DefenceCategory()
                    {
                        Code = typeContractDefence?.Element($"SERVICE_CATEGORY{prefix7}DEFENCE")?.Value
                    },
                    DefenceSupplies = ParseSuppliesTEDFormat(typeContractDefence?.Element("TYPE_SUPPLIES_CONTRACT")?.Attribute("VALUE")?.Value),
                    DefenceWorks = typeContractDefence?.Element("TYPE_WORK_CONTRACT")?.Element("DESIGN_EXECUTION") != null ? ProcurementProjectContractDefenceWorks.Design :
                                   typeContractDefence?.Element("TYPE_WORK_CONTRACT")?.Element("EXECUTION") != null ? ProcurementProjectContractDefenceWorks.Execution :
                                   typeContractDefence?.Element("TYPE_WORK_CONTRACT")?.Element("REALISATION_REQUIREMENTS_SPECIFIED_CONTRACTING_AUTHORITIES") != null ? ProcurementProjectContractDefenceWorks.Realisation : ProcurementProjectContractDefenceWorks.Undefined,
                    JointProcurement = defenceAuthorityInformation?.Element("TYPE_AND_ACTIVITIES_OR_CONTRACTING_ENTITY_AND_PURCHASING_ON_BEHALF")?.Element("PURCHASING_ON_BEHALF")?.Element("PURCHASING_ON_BEHALF_YES") != null,
                    CoPurchasers = ParseDefenceCoPurchasers(_nutsSchema, defenceAuthorityInformation?.Element("TYPE_AND_ACTIVITIES_OR_CONTRACTING_ENTITY_AND_PURCHASING_ON_BEHALF")?.Element("PURCHASING_ON_BEHALF")?.Element("PURCHASING_ON_BEHALF_YES")?.Descendants("CONTACT_DATA_OTHER_BEHALF_CONTRACTING_AUTORITHY")),

                    
                    Publish = ProcurementProjectContractPublish.ToTed
                },
                CommunicationInformation = ParseDefenceCommunicationInformation(_nutsSchema, defenceAuthorityInformation, noticeType),
                ProcurementObject = new ProcurementObject()
                {
                    MainCpvCode = new CpvCode
                    {
                        Code = descriptionContractInformation?.Element("CPV")?.Element("CPV_MAIN").Element("CPV_CODE")?.Attribute("CODE")?.Value ?? defenceContractInformation?.Element("CPV")?.Element("CPV_MAIN").Element("CPV_CODE")?.Attribute("CODE")?.Value,
                        VocCodes = descriptionContractInformation?.Element("CPV")?.Element("CPV_MAIN").Elements("CPV_SUPPLEMENTARY_CODE").Select(s => new VocCode { Code = s.Attribute("CODE")?.Value }).ToArray(),
                    },
                    EstimatedValue = ParseNationalEstimatedValue(descriptionContractInformation ?? defenceContractInformation),

                    TotalValue = ParseValueRangeContract(
                        valueElement: defenceContractInformation?.Element("TOTAL_FINAL_VALUE")?.Element("VALUE_COST") != null ?
                            defenceContractInformation?.Element("TOTAL_FINAL_VALUE")?.Element("VALUE_COST") :
                            defenceContractInformation?.Element("TOTAL_FINAL_VALUE")?.Element("COSTS_RANGE_AND_CURRENCY_WITH_VAT_RATE"),
                        rangeElement:
                            defenceContractInformation?.Element("TOTAL_FINAL_VALUE")?.Element("COSTS_RANGE_AND_CURRENCY_WITH_VAT_RATE")),

                    Defence = new ProcurementObjectDefence()
                    {
                        AdditionalCpvCodes = ParseAdditionalCpvCodes(descriptionContractInformation?.Element("CPV")),
                        AdditionalInformation = ParsePElements(defenceCcomplimentaryInformation?.Element("ADDITIONAL_INFORMATION") ?? defenceContractInformation?.Element("ADDITIONAL_INFORMATION")),
                        FrameworkAgreement = ParseDefenceFrameworkAgreement(descriptionContractInformation),
                        MainsiteplaceWorksDelivery = noticeType != NoticeContractType.DefencePriorInformation ? new string[] { descriptionContractInformation?.Element("LOCATION_NUTS")?.Element("LOCATION")?.Value } :
                                                     ParsePElements(descriptionContractInformation?.Element("SITE_OR_LOCATION")?.Element("LABEL")).Length > 0 ? ParsePElements(descriptionContractInformation?.Element("SITE_OR_LOCATION")?.Element("LABEL")) : new string[] { descriptionContractInformation?.Element("TYPE_CONTRACT_PLACE_DELIVERY_DEFENCE")?.Element("SITE_OR_LOCATION")?.Element("LABEL")?.Value },
                        NutsCodes = ParseNutsCodes(_nutsSchema, descriptionContractInformation?.Element("LOCATION_NUTS")),
                        OptionsAndVariants = new OptionsAndVariants()
                        {
                            Options = defenceContractInformation?.Element("QUANTITY_SCOPE")?.Element("OPTIONS") != null,
                            OptionsDays = ParseInt(defenceContractInformation?.Element("QUANTITY_SCOPE")?.Element("OPTIONS")?.Element("PROVISIONAL_TIMETABLE_DAY")?.Value),
                            OptionsDescription = ParsePElements(defenceContractInformation?.Element("QUANTITY_SCOPE")?.Element("OPTIONS")?.Element("OPTION_DESCRIPTION")),
                            OptionsMonths = ParseInt(defenceContractInformation?.Element("QUANTITY_SCOPE")?.Element("OPTIONS")?.Element("PROVISIONAL_TIMETABLE_MONTH")?.Value),
                            OptionType = defenceContractInformation?.Element("QUANTITY_SCOPE")?.Element("OPTIONS")?.Element("PROVISIONAL_TIMETABLE_MONTH") != null ? OptionsAndVariantsOptionType.Months :
                                         defenceContractInformation?.Element("QUANTITY_SCOPE")?.Element("OPTIONS")?.Element("PROVISIONAL_TIMETABLE_DAY") != null ? OptionsAndVariantsOptionType.Days :
                                         OptionsAndVariantsOptionType.Undefined,
                        },
                        Renewals = new DefenceRenewals()
                        {
                            Amount = ParseValueRangeContract(defenceContractInformation?.Element("QUANTITY_SCOPE")?.Element("RECURRENT_CONTRACT")),
                            CanBeRenewed = defenceContractInformation?.Element("QUANTITY_SCOPE")?.Element("RECURRENT_CONTRACT") != null,
                            SubsequentContract = new TimeFrame()
                            {
                                Type = defenceContractInformation?.Element("QUANTITY_SCOPE")?.Element("RECURRENT_CONTRACT")?.Element("TIME_FRAME_SUBSEQUENT_CONTRACTS_DAY") != null ? TimeFrameType.Days :
                                       defenceContractInformation?.Element("QUANTITY_SCOPE")?.Element("RECURRENT_CONTRACT")?.Element("TIME_FRAME_SUBSEQUENT_CONTRACTS_MONTH") != null ? TimeFrameType.Months : TimeFrameType.Undefined,
                                Days = ParseInt(defenceContractInformation?.Element("QUANTITY_SCOPE")?.Element("RECURRENT_CONTRACT")?.Element("TIME_FRAME_SUBSEQUENT_CONTRACTS_DAY")?.Value),
                                Months = ParseInt(defenceContractInformation?.Element("QUANTITY_SCOPE")?.Element("RECURRENT_CONTRACT")?.Element("TIME_FRAME_SUBSEQUENT_CONTRACTS_MONTH")?.Value)
                            }
                        },
                        Subcontract = ParseDefenceSubcontract(descriptionContractInformation),
                        TimeFrame = ParseTimeFrame(defenceContractInformation?.Element("PERIOD_WORK_DATE_STARTING") ?? defenceContractInformation?.Element("SCHEDULED_DATE_PERIOD")?.Element("PERIOD_WORK_DATE_STARTING")),
                        TotalQuantity = ParsePElements(defenceContractInformation?.Element("QUANTITY_SCOPE")?.Element("NATURE_QUANTITY_SCOPE")?.Element("TOTAL_QUANTITY_OR_SCOPE")),
                        TotalQuantityOrScope = ParseValueRangeContract(defenceContractInformation?.Element("QUANTITY_SCOPE")?.Element("NATURE_QUANTITY_SCOPE"))
                    },
                    ShortDescription = ParsePElements(descriptionContractInformation?.Element("SHORT_CONTRACT_DESCRIPTION") ?? defenceContractInformation?.Element("QUANTITY_SCOPE_WORKS_DEFENCE")?.Element("TOTAL_QUANTITY_OR_SCOPE")),
                    
                },
                ConditionsInformationDefence = ParseConditionsInformationDefence(defenceLeftiContract),
                ProcedureInformation = new ProcedureInformation()
                {
                    ProcedureType = ParseProcedureInformationProcedureType(typeOfProcedure?.Element("TYPE_OF_PROCEDURE_DETAIL_FOR_CONTRACT_NOTICE_DEFENCE") != null ? typeOfProcedure?.Element("TYPE_OF_PROCEDURE_DETAIL_FOR_CONTRACT_NOTICE_DEFENCE") : typeOfProcedure),

                    FrameworkAgreement = new FrameworkAgreementInformation()
                    {
                        IncludesFrameworkAgreement = defenceProcedureDefinitonContract?.Element("FRAMEWORK_AGREEMENT_IS_ESTABLISH")?.Attribute("VALUE")?.Value == "YES" ? true : false,

                    },
                    National = new ProcedureInformationNational()
                    {
                        OtherProcedure = ParsePElements(defenceProcedureDefinitonContract),
                        AdditionalProcedureInformation = ParsePElements(ContractRC?.Element("SELECTION_CRITERIA_INFORMATION"))
                    },
                    Defence = new ProcedureInformationDefence()
                    {
                        AwardCriteria = ParseDefenceAwardCriteria(defenceProcedureDefinitonContract),
                        CandidateNumberRestrictions = new CandidateNumberRestrictions()
                        {
                            EnvisagedMaximumNumber = ParseInt(defenceProcedureDefinitonContract?.Element("TYPE_OF_PROCEDURE_DEFENCE")?.Element("MAXIMUM_NUMBER_INVITED")?.Element("OPE_MAXIMUM_NUMBER")?.Value),
                            EnvisagedMinimumNumber = ParseInt(defenceProcedureDefinitonContract?.Element("TYPE_OF_PROCEDURE_DEFENCE")?.Element("MAXIMUM_NUMBER_INVITED")?.Element("OPE_MINIMUM_NUMBER")?.Value),
                            EnvisagedNumber = ParseInt(defenceProcedureDefinitonContract?.Element("TYPE_OF_PROCEDURE_DEFENCE")?.Element("MAXIMUM_NUMBER_INVITED")?.Element("OPE_ENVISAGED_NUMBER")?.Value),
                            ObjectiveCriteriaForChoosing = ParsePElements(defenceProcedureDefinitonContract?.Element("TYPE_OF_PROCEDURE_DEFENCE")?.Element("MAXIMUM_NUMBER_INVITED")?.Element("OPE_OBJECTIVE_CRITERIA")),
                            Selected = defenceProcedureDefinitonContract?.Element("TYPE_OF_PROCEDURE_DEFENCE")?.Element("MAXIMUM_NUMBER_INVITED") != null ?
                                        CandidateNumberRestrictionsSelected.EnvisagedNumber :
                                        defenceProcedureDefinitonContract?.Element("TYPE_OF_PROCEDURE_DEFENCE")?.Element("MAXIMUM_NUMBER_INVITED")?.Element("OPE_MINIMUM_NUMBER") != null ?
                                        CandidateNumberRestrictionsSelected.Range :
                                        CandidateNumberRestrictionsSelected.Undefined

                        }
                    },
                    JustificationForAcceleratedProcedure = ParsePElements(defenceProcedureDefinitonContract?.Element("TYPE_OF_PROCEDURE_DEFENCE")?.Element("TYPE_OF_PROCEDURE_DETAIL_FOR_CONTRACT_NOTICE_DEFENCE")?.Element("PT_ACCELERATED_RESTRICTED_CHOICE")?.Element("PTAR_JUSTIFICATION")),
                    AcceleratedProcedure = ParseDefenceAcceleratedProcedure(defenceProcedureDefinitonContract),
                    ElectronicAuctionWillBeUsed = defenceProcedureDefinitonContract?.Element($"AWARD_CRITERIA_CONTRACT{prefix1}_NOTICE_INFORMATION{prefix9}")?.Element($"{prefix8}IS_ELECTRONIC_AUCTION_USABLE")?.Attribute("VALUE")?.Value == "YES"
                },
                ConditionsInformationNational = new ConditionsInformationNational()
                {
                    ParticipantSuitabilityCriteria = ParsePElements(ContractRC?.Element("SELECTION_CRITERIA")),
                    RequiredCertifications = ParsePElements(ContractRC?.Element("SELECTION_CRITERIA_CERTIFICATIONS")),
                    ReservedForShelteredWorkshopOrProgram = defenceProcedureDefinitonContract?.Element("RESERVED_CONTRACTS")?.Attribute("VALUE").Value == "YES"
                },
                ConditionsInformation = new ConditionsInformation(),
                TenderingInformation = new TenderingInformation()
                {
                    TendersOrRequestsToParticipateDueDateTime = ParseDateTimeFromElements(administrativeInformationContractNoticeDefence?.Element("RECEIPT_LIMIT_DATE")),
                    Defence = new DefenceAdministrativeInformation()
                    {
                        DocumentPrice = new ValueContract()
                        {
                            Currency = administrativeInformationContractNoticeDefence?.Element("CONDITIONS_OBTAINING_SPECIFICATIONS")?.Element("DOCUMENT_COST")?.Attribute("CURRENCY")?.Value,
                            Value = ParseDouble(administrativeInformationContractNoticeDefence?.Element("CONDITIONS_OBTAINING_SPECIFICATIONS")?.Element("DOCUMENT_COST")?.Value)
                        },

                        PreviousPriorInformationNoticeOjsNumber = new OjsNumber()
                        {
                            Number = previousPublicationExist?.Element("PREVIOUS_PUBLICATION_NOTICE_F18")?.Element("NOTICE_NUMBER_OJ")?.Value,
                            Date = ParseDateTimeFromElements(previousPublicationExist?.Element("PREVIOUS_PUBLICATION_NOTICE_F18")?.Element("DATE_OJ"))
                        },
                        // PreviousContractType = FromTEDFormatPreviousContractType(previousPublicationExist?.Element("PREVIOUS_PUBLICATION_NOTICE_F18")?.Element("PREVIOUS_NOTICE_BUYER_PROFILE_F18")?.Attribute("CHOICE")?.Value),

                        HasPreviousExAnteOjsNumber = previousPublicationExist?.Element("EX_ANTE_NOTICE_INFORMATION") != null,
                        PreviousExAnteOjsNumber = new OjsNumber()
                        {
                            Number = previousPublicationExist?.Element("EX_ANTE_NOTICE_INFORMATION")?.Element("NOTICE_NUMBER_OJ")?.Value,
                            Date = ParseDateTimeFromElements(previousPublicationExist?.Element("EX_ANTE_NOTICE_INFORMATION")?.Element("DATE_OJ"))
                        },

                        HasPreviousContractNoticeOjsNumber = previousPublicationExist?.Element("CNT_NOTICE_INFORMATION_F18") != null,
                        PreviousContractNoticeOjsNumber = new OjsNumber()
                        {
                            Number = previousPublicationExist?.Element("CNT_NOTICE_INFORMATION_F18")?.Element("NOTICE_NUMBER_OJ")?.Value,
                            Date = ParseDateTimeFromElements(previousPublicationExist?.Element("CNT_NOTICE_INFORMATION_F18")?.Element("DATE_OJ"))
                        },

                        LanguageType = administrativeInformationContractNoticeDefence?.Element("LANGUAGE")?.Element("LANGUAGE_EC") != null ? DefenceAdministrativeInformationLanguageType.SelectedEu :
                                       administrativeInformationContractNoticeDefence?.Element("LANGUAGE")?.Element("LANGUAGE_ANY_EC")?.Attribute("VALUE")?.Value == "YES" ? DefenceAdministrativeInformationLanguageType.AnyOfficialEu
                                       : DefenceAdministrativeInformationLanguageType.Undefined,
                        Languages = ParseLanguages(administrativeInformationContractNoticeDefence),
                        OtherLanguage = administrativeInformationContractNoticeDefence?.Element("LANGUAGE")?.Element("LANGUAGE_OTHER") != null,
                        OtherLanguages = administrativeInformationContractNoticeDefence?.Element("LANGUAGE")?.Element("LANGUAGE_OTHER")?.Value,
                        PayableDocuments = administrativeInformationContractNoticeDefence?.Element("CONDITIONS_OBTAINING_SPECIFICATIONS")?.Element("PAYABLE_DOCUMENTS") != null,
                        PaymentTermsAndMethods = ParsePElements(administrativeInformationContractNoticeDefence?.Element("CONDITIONS_OBTAINING_SPECIFICATIONS")?.Element("PAYABLE_DOCUMENTS")?.Element("DOCUMENT_METHOD_OF_PAYMENT")),
                        TimeLimitForReceipt = ParseDateTimeFromElements(administrativeInformationContractNoticeDefence?.Element("PREVIOUS_PUBLICATION_INFORMATION_NOTICE_F18")?.Element("CONDITIONS_OBTAINING_SPECIFICATIONS")?.Element("TIME_LIMIT")),
                        PreviousPublicationExists = previousPublicationExist != null

                    },
                    
                },
                ProceduresForReview = ParseProceduresForReview(reviewBody, null),
                ComplementaryInformation = new ComplementaryInformation()
                {
                    Defence = new ComplementaryInformationDefence()
                    {

                        EuFunds = new EuFunds()
                        {
                            ProcurementRelatedToEuProgram = defenceCcomplimentaryInformation?.Element("RELATES_TO_EU_PROJECT_YES") != null,
                        },
                        TaxLegislationUrl = othInfoPriorInformation?.Element("INFORMATION_REGULATORY_FRAMEWORK")?.Element("TAX_LEGISLATION")?.Element("TAX_LEGISLATION_VALUE")?.Value,
                        TaxLegislationInfoProvided = othInfoPriorInformation?.Element("INFORMATION_REGULATORY_FRAMEWORK")?.Element("TAX_LEGISLATION")?.Element("CONTACT_DATA") != null,
                        TaxLegislation = ParseContractingBodyInformation(_nutsSchema, othInfoPriorInformation?.Element("INFORMATION_REGULATORY_FRAMEWORK")?.Element("TAX_LEGISLATION")?.Element("CONTACT_DATA")),

                        EnvironmentalProtectionUrl = othInfoPriorInformation?.Element("INFORMATION_REGULATORY_FRAMEWORK")?.Element("ENVIRONMENTAL_PROTECTION_LEGISLATION")?.Element("ENVIRONMENTAL_PROTECTION_LEGISLATION_VALUE")?.Value,
                        EnvironmentalProtectionInfoProvided = othInfoPriorInformation?.Element("INFORMATION_REGULATORY_FRAMEWORK")?.Element("ENVIRONMENTAL_PROTECTION_LEGISLATION")?.Element("CONTACT_DATA") != null,
                        EnvironmentalProtection = ParseContractingBodyInformation(_nutsSchema, othInfoPriorInformation?.Element("INFORMATION_REGULATORY_FRAMEWORK")?.Element("ENVIRONMENTAL_PROTECTION_LEGISLATION")?.Element("CONTACT_DATA")),

                        EmploymentProtectionUrl = othInfoPriorInformation?.Element("INFORMATION_REGULATORY_FRAMEWORK")?.Element("EMPLOYMENT_PROTECTION_WORKING_CONDITIONS")?.Element("EMPLOYMENT_PROTECTION_WORKING_CONDITIONS_VALUE")?.Value,
                        EmploymentProtectionInfoProvided = othInfoPriorInformation?.Element("INFORMATION_REGULATORY_FRAMEWORK")?.Element("EMPLOYMENT_PROTECTION_WORKING_CONDITIONS")?.Element("CONTACT_DATA") != null,
                        EmploymentProtection = ParseContractingBodyInformation(_nutsSchema, reviewBody ?? othInfoPriorInformation?.Element("INFORMATION_REGULATORY_FRAMEWORK")?.Element("EMPLOYMENT_PROTECTION_WORKING_CONDITIONS")?.Element("CONTACT_DATA"))

                    },
                    AdditionalInformation = ParsePElements(defenceCcomplimentaryInformation?.Element("ADDITIONAL_INFORMATION") ?? othInfoPriorInformation?.Element("ADDITIONAL_INFORMATION")),
                },
                LotsInfo = ParseDefenceLotsInfo(descriptionContractInformation),
                AttachmentInformation = new AttachmentInformation(),
                Attachments = new AttachmentViewModel[0],
                
                DatePublished = importedNotice.HilmaPublishedDate,
                ContractAwardsDefence = ParseContractAwardsDefence(defenceContract?.Descendants("AWARD_OF_CONTRACT_DEFENCE"), defenceContractInformation, _nutsSchema),
                Annexes = ParseDefenceAnnex(defenceProcedureDefinitonContract?.Element("TYPE_OF_PROCEDURE_CONTRACT_AWARD_DEFENCE")?.Element("F18_PT_NEGOTIATED_WITHOUT_PUBLICATION_CONTRACT_NOTICE"))

            };

            if (notice.Project.Organisation.ContractingAuthorityType == OrganisationContractContractingAuthorityType.OtherType)
            {
                if (string.IsNullOrEmpty(notice.Project.Organisation.OtherContractingAuthorityType))
                {
                    notice.Project.Organisation.OtherContractingAuthorityType = "Ei määritelty";
                }
            }
            if (noticeType == NoticeContractType.DefenceContract || noticeType == NoticeContractType.DefencePriorInformation)
            {
                notice.ObjectDescriptions = ParseDefenceObjectDescriptions(_nutsSchema, descriptionContractInformation).ToArray();
            }
            if (noticeType == NoticeContractType.DefenceContractAward)
            {
                notice.ObjectDescriptions = new ObjectDescription[] { };
            }

            notice.Project.Organisation.Information.NutsCodes = ParseSingleNutsCode(descriptionContractInformation?.Element("DOMESTIC_OBJECT_INFORMATION")?.Element(_nutsSchema + "NUTS"));

            return notice;
        }

        private static LotsInfo ParseDefenceLotsInfo(XElement descriptionContractInformation)
        {
            var formType = "F16";
            if (descriptionContractInformation?.Element("F17_DIVISION_INTO_LOTS") != null)
            {
                formType = "F17";
            }
            if (formType == "F16")
            {
                descriptionContractInformation = descriptionContractInformation?.Element("QUANTITY_SCOPE_WORKS_DEFENCE");
            }
            return new LotsInfo()
            {
                DivisionLots = descriptionContractInformation?.Element($"{formType}_DIVISION_INTO_LOTS")?.Element("DIV_INTO_LOT_YES") != null ? true :
                    descriptionContractInformation?.Element($"{formType}_DIVISION_INTO_LOTS")?.Element($"{formType}_DIV_INTO_LOT_YES") != null ? true : false,
                LotsSubmittedFor = descriptionContractInformation?.Element($"{formType}_DIVISION_INTO_LOTS")?.Element($"{formType}_DIV_INTO_LOT_YES")?.Attribute("VALUE")?.Value == "ALL_LOTS" ? LotsInfoLotsSubmittedFor.LotsAll :
                                                   descriptionContractInformation?.Element($"{formType}_DIVISION_INTO_LOTS")?.Element($"{formType}_DIV_INTO_LOT_YES")?.Attribute("VALUE")?.Value == "ONE_OR_MORE_LOT" ? LotsInfoLotsSubmittedFor.LotsMax :
                                                   descriptionContractInformation?.Element($"{formType}_DIVISION_INTO_LOTS")?.Element($"{formType}_DIV_INTO_LOT_YES")?.Attribute("VALUE")?.Value == "ONE_LOT_ONLY" ? LotsInfoLotsSubmittedFor.LotOneOnly :
                                                   LotsInfoLotsSubmittedFor.Undefined
            };
        }

        private bool ParseDefenceAcceleratedProcedure(XElement defenceProcedureDefinitonContract)
        {
            var typeOfContractDefence = defenceProcedureDefinitonContract?.Element("TYPE_OF_PROCEDURE_DEFENCE")?.Element("TYPE_OF_PROCEDURE_DETAIL_FOR_CONTRACT_NOTICE_DEFENCE");
            var typeOfAwardContractDefence = defenceProcedureDefinitonContract?.Element("TYPE_OF_PROCEDURE_CONTRACT_AWARD_DEFENCE");

            if (typeOfContractDefence != null)
            {
                if (typeOfContractDefence?.Descendants()?.First()?.Name?.LocalName?.Contains("ACCELERATED") == true)
                {
                    return true;
                }
            }

            if (typeOfAwardContractDefence != null)
            {
                if (typeOfAwardContractDefence?.Descendants()?.First()?.Name?.LocalName?.Contains("ACCELERATED") == true)
                {
                    return true;
                }
            }
            return false;

        }

        //public static PreviousContractType FromTEDFormatPreviousContractType(string value)
        //{
        //    switch (value)
        //    {
        //        case "PRIOR_INFORMATION_NOTICE":
        //            return PreviousContractType.PriorInformation;
        //        default:
        //            return PreviousContractType.Undefined;
        //    }
        //}

        public static AwardCriteriaDefence ParseDefenceAwardCriteria(XElement defenceProcedureDefinitonContract)
        {


            var awardCriteriaDetail = defenceProcedureDefinitonContract?.Element("AWARD_CRITERIA_CONTRACT_NOTICE_INFORMATION")?.Element("AWARD_CRITERIA_DETAIL");
            var tender = "MOST_ECONOMICALLY_ADVANTAGEOUS_TENDER";
            if (awardCriteriaDetail == null)
            {
                awardCriteriaDetail = defenceProcedureDefinitonContract?.Element("AWARD_CRITERIA_CONTRACT_AWARD_NOTICE_INFORMATION_DEFENCE")?.Element("AWARD_CRITERIA_DETAIL_F18");
                tender = "MOST_ECONOMICALLY_ADVANTAGEOUS_TENDER_SHORT";
            }

            return new AwardCriteriaDefence()
            {
                EconomicCriteriaTypes = awardCriteriaDetail?.Element("CRITERIA_STATED_BELOW") != null ? AwardCriteriaDefenceEconomicCriteriaTypes.CriteriaBelow :
                                        awardCriteriaDetail?.Element(tender)?.Element("CRITERIA_STATED_IN_OTHER_DOCUMENT") != null ?
                                        AwardCriteriaDefenceEconomicCriteriaTypes.CriteriaElsewhere :
                                        AwardCriteriaDefenceEconomicCriteriaTypes.Undefined,

                CriterionTypes = awardCriteriaDetail?.Element("LOWEST_PRICE") != null ? AwardCriteriaDefenceCriterionTypes.LowestPrice :
                                 awardCriteriaDetail?.Element(tender) != null ? AwardCriteriaDefenceCriterionTypes.EconomicallyAdvantageous :
                                 AwardCriteriaDefenceCriterionTypes.Undefined,

                Criteria = ParseNationalAwardCriteria(defenceProcedureDefinitonContract?.Descendants("CRITERIA_DEFINITION"))
            };
        }

        private static ContractAwardDefence[] ParseContractAwardsDefence(IEnumerable<XElement> awardOfContractDefence, XElement defenceContractInformation, XNamespace _nutsSchema)
        {

            return awardOfContractDefence?.Select(award =>
               new ContractAwardDefence()
               {

                   LotNumber = award.Element("LOT_NUMBER")?.Value,
                   ContractNumber = award.Element("CONTRACT_NUMBER")?.Value,
                   AllOrCertainSubcontractsWillBeAwarded = award?.Element("MORE_INFORMATION_TO_SUB_CONTRACTED")?.Element("CONTRACT_LIKELY_SUB_CONTRACTED_WITH_DEFENCE")?.Element("SUBCONTRACT_DEFENCE")?.Element("SUBCONTRACT_AWARD_PART") != null,
                   AnnualOrMonthlyValue = new TimeFrame()
                   {
                       Type = award?.Element("CONTRACT_VALUE_INFORMATION")?.Element("MORE_INFORMATION_IF_ANNUAL_MONTHLY")?.Element("NUMBER_OF_YEARS") != null ?
                              TimeFrameType.Years : award?.Element("CONTRACT_VALUE_INFORMATION")?.Element("MORE_INFORMATION_IF_ANNUAL_MONTHLY")?.Element("NUMBER_OF_MONTHS") != null ?
                              TimeFrameType.Months : TimeFrameType.Undefined,
                       Months = ParseInt(award?.Element("CONTRACT_VALUE_INFORMATION")?.Element("MORE_INFORMATION_IF_ANNUAL_MONTHLY")?.Element("NUMBER_OF_MONTHS")?.Value),
                       Years = ParseInt(award?.Element("CONTRACT_VALUE_INFORMATION")?.Element("MORE_INFORMATION_IF_ANNUAL_MONTHLY")?.Element("NUMBER_OF_YEARS")?.Value)
                   },
                   ContractAwardDecisionDate = ParseDateTimeFromElements(award?.Element("CONTRACT_AWARD_DATE")),
                   Contractor = ParseContractor(award?.Element("ECONOMIC_OPERATOR_NAME_ADDRESS")),
                   EstimatedValue = ParseValueContract(award?.Element("CONTRACT_VALUE_INFORMATION")?.Element("INITIAL_ESTIMATED_TOTAL_VALUE_CONTRACT")),
                   FinalTotalValue = ParseValueContract(award?.Element("TOTAL_FINAL_VALUE")?.Element("COSTS_RANGE_AND_CURRENCY_WITH_VAT_RATE") != null ?
                   award?.Element("TOTAL_FINAL_VALUE")?.Element("COSTS_RANGE_AND_CURRENCY_WITH_VAT_RATE")?.Element("") : award?.Element("CONTRACT_VALUE_INFORMATION")?.Element("COSTS_RANGE_AND_CURRENCY_WITH_VAT_RATE")),
                   LikelyToBeSubcontracted = ParseLikelyToBeSubcontracted(award),

                   ContractValueType = defenceContractInformation?.Element("TOTAL_FINAL_VALUE")?.Element("COSTS_RANGE_AND_CURRENCY_WITH_VAT_RATE")?.Element("LOW_VALUE") != null ||
                                      award?.Element("CONTRACT_VALUE_INFORMATION")?.Element("COSTS_RANGE_AND_CURRENCY_WITH_VAT_RATE")?.Element("RANGE_VALUE_COST")?.Element("LOW_VALUE") != null ? ContractAwardDefenceContractValueType.Range : ContractAwardDefenceContractValueType.Exact,

                   HighestOffer = new ValueContract() { Value = ParseDouble(defenceContractInformation?.Element("TOTAL_FINAL_VALUE")?.Element("COSTS_RANGE_AND_CURRENCY_WITH_VAT_RATE")?.Element("RANGE_VALUE_COST")?.Element("HIGH_VALUE")?.Value) },
                   LowestOffer = new ValueContract() { Value = ParseDouble(defenceContractInformation?.Element("TOTAL_FINAL_VALUE")?.Element("COSTS_RANGE_AND_CURRENCY_WITH_VAT_RATE")?.Element("RANGE_VALUE_COST")?.Element("LOW_VALUE")?.Value) },
                   LotTitle = award?.Element("CONTRACT_TITLE")?.Value,
                   NumberOfTenders = ParseNumberOfTenders(award),
                   ProportionOfValue = ParseDouble(award?.Element("MORE_INFORMATION_TO_SUB_CONTRACTED")?.Element("CONTRACT_LIKELY_SUB_CONTRACTED_WITH_DEFENCE")?.Element("EXCLUDING_VAT_PRCT")?.Value),
                   ShareOfContractWillBeSubcontracted = award?.Element("MORE_INFORMATION_TO_SUB_CONTRACTED")?.Element("CONTRACT_LIKELY_SUB_CONTRACTED_WITH_DEFENCE")?.Element("SUBCONTRACT_DEFENCE")?.Element("SUBCONTRACT_SHARE") != null,

                   ShareOfContractWillBeSubcontractedMaxPercentage = ParseDouble(award?.Element("MORE_INFORMATION_TO_SUB_CONTRACTED")?.Element("CONTRACT_LIKELY_SUB_CONTRACTED_WITH_DEFENCE")?.Element("SUBCONTRACT_DEFENCE")?.Element("SUBCONTRACT_SHARE")?.Element("MAX_PERCENTAGE")?.Value),
                   ShareOfContractWillBeSubcontractedMinPercentage = ParseDouble(award?.Element("MORE_INFORMATION_TO_SUB_CONTRACTED")?.Element("CONTRACT_LIKELY_SUB_CONTRACTED_WITH_DEFENCE")?.Element("SUBCONTRACT_DEFENCE")?.Element("SUBCONTRACT_SHARE")?.Element("MIN_PERCENTAGE")?.Value),
                   SubcontractingDescription = ParsePElements(award?.Element("MORE_INFORMATION_TO_SUB_CONTRACTED")?.Element("CONTRACT_LIKELY_SUB_CONTRACTED_WITH_DEFENCE")?.Element("ADDITIONAL_INFORMATION")),
                   ValueOfSubcontract = ParseValueContract(award?.Element("MORE_INFORMATION_TO_SUB_CONTRACTED")?.Element("CONTRACT_LIKELY_SUB_CONTRACTED_WITH_DEFENCE")?.Element("EXCLUDING_VAT_VALUE")),
                   ValueOfSubcontractNotKnown = award?.Element("MORE_INFORMATION_TO_SUB_CONTRACTED")?.Element("CONTRACT_LIKELY_SUB_CONTRACTED_WITH_DEFENCE")?.Element("UNKNOWN_VALUE") != null,
                   
               }).ToArray() ?? new ContractAwardDefence[0];


        }

        private static bool ParseLikelyToBeSubcontracted(XElement award)
        {
            if (award?.Element("MORE_INFORMATION_TO_SUB_CONTRACTED") != null)
            {
                return award?.Element("MORE_INFORMATION_TO_SUB_CONTRACTED")?.Element("CONTRACT_LIKELY_SUB_CONTRACTED_WITH_DEFENCE") != null;
            }

            return false;
        }

        private static ContractorContactInformation ParseContractor(XElement economicOperatorNameAddress)
        {
            XNamespace nutsSchema = "http://publications.europa.eu/resource/schema/ted/2016/nuts";
            var element = economicOperatorNameAddress;
            if (economicOperatorNameAddress?.Element("CONTACT_DATA_WITHOUT_RESPONSIBLE_NAME") != null)
            {
                element = economicOperatorNameAddress?.Element("CONTACT_DATA_WITHOUT_RESPONSIBLE_NAME");
            }

            return new ContractorContactInformation()
            {

                Email = element?.Element("E_MAILS")?.Element("E_MAIL")?.Value,
                MainUrl = element?.Element("URL")?.Value,
                NationalRegistrationNumber = element?.Element("ORGANISATION")?.Element("NATIONALID")?.Value ?? element?.Element("NATIONALID")?.Value,
                OfficialName = element?.Element("ORGANISATION")?.Element("OFFICIALNAME")?.Value ?? element?.Element("OFFICIALNAME")?.Value,
                PostalAddress = ParsePostalAddress(element),
                TelephoneNumber = element?.Element("PHONE")?.Value,
                
                NutsCodes = ParseNutsCodes(nutsSchema, element)
            };
        }

        private static SubcontractingInformation ParseDefenceSubcontract(XElement descriptionContractInformation)
        {
            var subcontracting = descriptionContractInformation?.Element("SUBCONTRACTING");
            if (subcontracting == null)
            {
                return null;
            }
            return new SubcontractingInformation()
            {
                CaMayOblige = subcontracting.Element("SUBCONTRACT_AWARD_PART")?.Attribute("VALUE")?.Value == "YES",
                SuccessfulTenderer = subcontracting.Element("SUBCONTRACT_SHARE")?.Attribute("VALUE")?.Value == "YES",
                SuccessfulTendererMax = ParseDouble(subcontracting.Element("MAX_PERCENTAGE")?.Value),
                SuccessfulTendererMin = ParseDouble(subcontracting.Element("MIN_PERCENTAGE")?.Value),
                SuccessfulTendererToSpecify = subcontracting.Element("IDENTIFY_SUBCONTRACT")?.Attribute("VALUE")?.Value == "YES",
                TendererHasToIndicateChange = subcontracting.Element("INDICATE_ANY_CHANGE")?.Attribute("VALUE")?.Value == "YES",
                TendererHasToIndicateShare = subcontracting.Element("INDICATE_ANY_SHARE")?.Attribute("VALUE")?.Value == "YES"

            };
        }

        private static ConditionsInformationDefence ParseConditionsInformationDefence(XElement defenceLeftiContract)
        {
            var contractRelatingConditions = defenceLeftiContract?.Element("CONTRACT_RELATING_CONDITIONS") ?? defenceLeftiContract?.Element("LEFTI_PRIOR_INFORMATION");
            var f17ConditionsForParticipation = defenceLeftiContract?.Element("F17_CONDITIONS_FOR_PARTICIPATION");

            return new ConditionsInformationDefence()
            {
                DepositsRequired = ParsePElements(contractRelatingConditions?.Element("DEPOSITS_GUARANTEES_REQUIRED")),
                EconomicCriteriaOfEconomicOperators = ParsePElements(f17ConditionsForParticipation?.Element("F17_ECONOMIC_FINANCIAL_CAPACITY")?.Element("EAF_CAPACITY_INFORMATION")),
                EconomicCriteriaOfEconomicOperatorsMinimum = ParsePElements(f17ConditionsForParticipation?.Element("F17_ECONOMIC_FINANCIAL_CAPACITY")?.Element("EAF_CAPACITY_MIN_LEVEL")),
                EconomicCriteriaOfSubcontractors = ParsePElements(f17ConditionsForParticipation?.Element("F17_ECONOMIC_FINANCIAL_CAPACITY_SUBCONTRACTORS")?.Element("EAF_CAPACITY_INFORMATION")),
                EconomicCriteriaOfSubcontractorsMinimum = ParsePElements(f17ConditionsForParticipation?.Element("F17_ECONOMIC_FINANCIAL_CAPACITY_SUBCONTRACTORS")?.Element("EAF_CAPACITY_MIN_LEVEL")),
                FinancingConditions = ParsePElements(contractRelatingConditions?.Element("MAIN_FINANCING_CONDITIONS")),
                LegalFormTaken = ParsePElements(contractRelatingConditions?.Element("LEGAL_FORM")),
                OtherParticularConditions = ParsePElements(contractRelatingConditions?.Element("EXISTENCE_OTHER_PARTICULAR_CONDITIONS")),
                PersonalSituationOfEconomicOperators = ParsePElements(f17ConditionsForParticipation?.Element("ECONOMIC_OPERATORS_PERSONAL_SITUATION")),
                PersonalSituationOfSubcontractors = ParsePElements(f17ConditionsForParticipation?.Element("ECONOMIC_OPERATORS_PERSONAL_SITUATION_SUBCONTRACTORS")),
                RestrictedToParticularProfession = defenceLeftiContract?.Element("SERVICES_CONTRACTS_SPECIFIC_CONDITIONS")?.Element("EXECUTION_SERVICE_RESERVED_PARTICULAR_PROFESSION") != null,
                RestrictedToParticularProfessionLaw = ParsePElements(defenceLeftiContract?.Element("SERVICES_CONTRACTS_SPECIFIC_CONDITIONS")?.Element("EXECUTION_SERVICE_RESERVED_PARTICULAR_PROFESSION")),
                StaffResponsibleForExecution = defenceLeftiContract?.Element("SERVICES_CONTRACTS_SPECIFIC_CONDITIONS")?.Element("REQUESTS_NAMES_PROFESSIONAL_QUALIFICATIONS")?.Attribute("VALUE")?.Value == "YES",
                TechnicalCriteriaOfEconomicOperators = ParsePElements(f17ConditionsForParticipation?.Element("TECHNICAL_CAPACITY_LEFTI")?.Element("T_CAPACITY_INFORMATION")),
                TechnicalCriteriaOfEconomicOperatorsMinimum = ParsePElements(f17ConditionsForParticipation?.Element("TECHNICAL_CAPACITY_LEFTI")?.Element("T_CAPACITY_MIN_LEVEL")),
                TechnicalCriteriaOfSubcontractors = ParsePElements(f17ConditionsForParticipation?.Element("TECHNICAL_CAPACITY_LEFTI_SUBCONTRACTORS")?.Element("T_CAPACITY_INFORMATION")),
                TechnicalCriteriaOfSubcontractorsMinimum = ParsePElements(f17ConditionsForParticipation?.Element("TECHNICAL_CAPACITY_LEFTI_SUBCONTRACTORS")?.Element("T_CAPACITY_MIN_LEVEL")),
                
            };
        }

        private static FrameworkAgreementInformation ParseDefenceFrameworkAgreement(XElement descriptionContractInformation)
        {
            XElement f17Framework = descriptionContractInformation?.Element("F17_FRAMEWORK");

            if (descriptionContractInformation?.Element("FRAMEWORK_AGREEMENT") != null)
            {

                return new FrameworkAgreementInformation()
                {
                    IncludesFrameworkAgreement = descriptionContractInformation?.Element("FRAMEWORK_AGREEMENT")?.Attribute("VALUE")?.Value == "YES",
                };
            }

            if (f17Framework == null)
            {
                return null;
            };

            return new FrameworkAgreementInformation()
            {
                IncludesFrameworkAgreement = descriptionContractInformation.Element("NOTICE_INVOLVES_DEFENCE") != null || descriptionContractInformation?.Element("FRAMEWORK_AGREEMENT")?.Attribute("VALUE")?.Value == "YES",
                IncludesConclusionOfFrameworkAgreement = descriptionContractInformation.Element("NOTICE_INVOLVES_DESC_DEFENCE") != null,
                FrameworkAgreementType = f17Framework.Element("SEVERAL_OPERATORS") != null ? FrameworkAgreementInformationFrameworkAgreementType.FrameworkSeveral :
                                         f17Framework.Element("SINGLE_OPERATOR") != null ? FrameworkAgreementInformationFrameworkAgreementType.FrameworkSingle :
                                         FrameworkAgreementInformationFrameworkAgreementType.Undefined,
                Duration = new TimeFrame()
                {
                    Type = f17Framework.Element("DURATION_FRAMEWORK_YEAR") != null ? TimeFrameType.Years :
                            f17Framework.Element("DURATION_FRAMEWORK_MONTH") != null ? TimeFrameType.Months :
                            TimeFrameType.Undefined,
                    Years = ParseInt(f17Framework.Element("DURATION_FRAMEWORK_YEAR")?.Value),
                    Months = ParseInt(f17Framework.Element("DURATION_FRAMEWORK_MONTH")?.Value),
                    CanBeRenewed = f17Framework?.Element("RECURRENT_CONTRACT") != null,
                    BeginDate = ParseDate(f17Framework.Element("DATE_START")?.Value),
                    EndDate = ParseDate(f17Framework.Element("DATE_END")?.Value),


                },
                EnvisagedNumberOfParticipants = f17Framework.Element("NUMBER_PARTICIPANTS") != null ? ParseInt(f17Framework.Element("NUMBER_PARTICIPANTS")?.Value) :
                                                 f17Framework.Element("MAX_NUMBER_PARTICIPANTS") != null ? ParseInt(f17Framework.Element("MAX_NUMBER_PARTICIPANTS")?.Value) : (int?)null,
                EstimatedTotalValue = ParseValueRangeContract(f17Framework.Element("TOTAL_ESTIMATED")),
                FrameworkEnvisagedType = f17Framework.Element("NUMBER_PARTICIPANTS") != null ? FrameworkAgreementInformationFrameworkEnvisagedType.FrameworkEnvisagedExact :
                                         f17Framework.Element("MAX_NUMBER_PARTICIPANTS") != null ? FrameworkAgreementInformationFrameworkEnvisagedType.FrameworkEnvisagedMax :
                                         FrameworkAgreementInformationFrameworkEnvisagedType.Undefined,
                FrequencyAndValue = ParsePElements(f17Framework?.Element("TOTAL_ESTIMATED")?.Element("FREQUENCY_AWARDED_CONTRACTS")),
                JustificationForDurationOverSevenYears = ParsePElements(f17Framework?.Element("JUSTIFICATION")),
            };
        }



        private Annex ParseAnnex(ProcedureInformationProcedureType ProcedureInformationProcedureType, XElement procedure, ProcurementProjectContractProcurementCategory procurementCategory)
        {
            var annex = new Annex() { D1 = new AnnexD1() };
            if (procedure != null)
            {
                XElement ptAwardWithoutCall = procedure?.Element("PT_AWARD_CONTRACT_WITHOUT_CALL") != null ? procedure?.Element("PT_AWARD_CONTRACT_WITHOUT_CALL") :
                                              procedure?.Element("DIRECTIVE_2014_24_EU")?.Element("PT_AWARD_CONTRACT_WITHOUT_CALL");
                XElement ptNegotiatedWithoutpublicataion = procedure?.Element("DIRECTIVE_2014_24_EU")?.Element("PT_NEGOTIATED_WITHOUT_PUBLICATION") != null ?
                    procedure?.Element("DIRECTIVE_2014_24_EU")?.Element("PT_NEGOTIATED_WITHOUT_PUBLICATION") :
                    procedure?.Element("DIRECTIVE_2009_81_EC")?.Element("PT_NEGOTIATED_WITHOUT_PUBLICATION") != null ? procedure?.Element("DIRECTIVE_2009_81_EC")?.Element("PT_NEGOTIATED_WITHOUT_PUBLICATION") :
                    procedure?.Element("DIRECTIVE_2014_25_EU")?.Element("PT_NEGOTIATED_WITHOUT_PUBLICATION");
                XElement ptAwardWithoutCallElement = null;
                if (ptAwardWithoutCall != null)
                {
                    ptAwardWithoutCallElement = ptAwardWithoutCall;
                }

                if (ptNegotiatedWithoutpublicataion != null)
                {
                    ptAwardWithoutCallElement = ptNegotiatedWithoutpublicataion;
                }

                if (procurementCategory == ProcurementProjectContractProcurementCategory.Public && ptAwardWithoutCallElement != null)
                {

                    annex.D1 = new AnnexD1()
                    {
                        Justification = ParsePElements(ptAwardWithoutCallElement.Element("D_JUSTIFICATION")),
                        AdvantageousPurchaseReason =
                            ptAwardWithoutCallElement.Element("D_FROM_LIQUIDATOR_CREDITOR") != null
                                ? AnnexD1AdvantageousPurchaseReason.DFromReceivers
                                : ptAwardWithoutCallElement.Element("D_FROM_WINDING_PROVIDER") != null
                                    ? AnnexD1AdvantageousPurchaseReason.DFromWindingSupplier
                                    : AnnexD1AdvantageousPurchaseReason.Undefined,
                        ExtremeUrgency = ptAwardWithoutCallElement.Element("D_EXTREME_URGENCY") != null,
                        ReasonForNoCompetition = ptAwardWithoutCallElement.Element("D_TECHNICAL") != null
                            ? AnnexD1ReasonForNoCompetition.DTechnical
                            : ptAwardWithoutCallElement.Element("D_ARTISTIC") != null
                                ? AnnexD1ReasonForNoCompetition.DArtistic
                                : ptAwardWithoutCallElement.Element("D_PROTECT_RIGHTS") != null
                                    ? AnnexD1ReasonForNoCompetition.DProtectRights
                                    : AnnexD1ReasonForNoCompetition.Undefined,
                        ProcedureType = ptAwardWithoutCallElement.Element("D_PROC_OPEN") != null
                            ? AnnexD1ProcedureType.DProcOpen
                            : ptAwardWithoutCallElement.Element("D_PROC_RESTRICTED") != null
                                ? AnnexD1ProcedureType.DProcRestricted
                                : AnnexD1ProcedureType.Undefined,
                        RepetitionExisting = ptAwardWithoutCallElement.Element("D_REPETITION_EXISTING") != null ||
                                             ptAwardWithoutCallElement?.Element("D_ACCORDANCE_ARTICLE")
                                                 ?.Element("D_REPETITION_EXISTING") != null
                    };

                    if (annex.D1.ProcedureType != AnnexD1ProcedureType.Undefined)
                    {
                        annex.D1.NoTenders = true;
                    }
                    if (annex.D1.ReasonForNoCompetition == AnnexD1ReasonForNoCompetition.Undefined)
                    {
                        annex.D1.AdditionalDeliveries = ptAwardWithoutCallElement?.Element("D_ACCORDANCE_ARTICLE")?.Element("D_ADD_DELIVERIES_ORDERED") != null;
                    }

                    if (annex.D1.ReasonForNoCompetition != AnnexD1ReasonForNoCompetition.Undefined)
                    {
                        annex.D1.ProvidedByOnlyParticularOperator = true;
                    }

                    if (annex.D1.AdvantageousPurchaseReason != AnnexD1AdvantageousPurchaseReason.Undefined)
                    {
                        annex.D1.AdvantageousTerms = true;
                    }
                }

                if (ProcedureInformationProcedureType == ProcedureInformationProcedureType.AwardWoPriorPubD4 ||
                    ProcedureInformationProcedureType == ProcedureInformationProcedureType.AwardWoPriorPubD4Other)
                {
                    XElement ptAwardWoPublication = procedure?.Element("PT_AWARD_CONTRACT_WITHOUT_PUBLICATION");
                    var element = ptAwardWoPublication?.Element("D_ACCORDANCE_ARTICLE");
                    var reason = GetReasonAnnexD4(element);

                    annex.D4 = new AnnexD4()
                    {
                        Justification = ParsePElements(ptAwardWoPublication?.Element("D_JUSTIFICATION")),
                        ReasonForNoCompetition = reason,
                        NoTenders = element?.Element("D_NO_TENDERS_REQUESTS") != null,
                        ProvidedByOnlyParticularOperator = reason != AnnexD4ReasonForNoCompetition.Undefined,

                    };
                }

                if (procurementCategory == ProcurementProjectContractProcurementCategory.Utility)
                {
                    var element = ptAwardWithoutCallElement?.Element("D_ACCORDANCE_ARTICLE");
                    annex.D2 = new AnnexD2()
                    {

                        Justification = ParsePElements(ptAwardWithoutCallElement?.Element("D_JUSTIFICATION")),
                        RepetitionExisting = ptAwardWithoutCallElement?.Element("D_REPETITION_EXISTING") != null ||
                                                 ptAwardWithoutCallElement?.Element("D_ACCORDANCE_ARTICLE")
                                                     ?.Element("D_REPETITION_EXISTING") != null,
                        ReasonForNoCompetition = GetReasonAnnexD2(element),
                        ExtremeUrgency = element?.Element("D_EXTREME_URGENCY")?.Name?.LocalName == "D_EXTREME_URGENCY"
                    };

                }

                if (procurementCategory == ProcurementProjectContractProcurementCategory.Defence)
                {
                    var element = ptAwardWithoutCallElement?.Element("D_ACCORDANCE_ARTICLE");
                    annex.D3 = new AnnexD3()
                    {

                        Justification = ParsePElements(ptAwardWithoutCallElement.Element("D_JUSTIFICATION")),
                        RepetitionExisting = ptAwardWithoutCallElement.Element("D_REPETITION_EXISTING") != null ||
                                                 ptAwardWithoutCallElement?.Element("D_ACCORDANCE_ARTICLE")
                                                     ?.Element("D_REPETITION_EXISTING") != null,
                        ReasonForNoCompetition = GetReasonAnnexD3(element)

                    };

                }


            }

            return annex;
        }

        private static AnnexD3ReasonForNoCompetition GetReasonAnnexD3(XElement element)
        {
            AnnexD3ReasonForNoCompetition reason = AnnexD3ReasonForNoCompetition.Undefined;

            if (element != null)
            {
                if (element.Element("D_TECHNICAL") != null)
                    reason = AnnexD3ReasonForNoCompetition.DTechnical;
                if (element.Element("D_PROTECT_RIGHTS") != null)
                    reason = AnnexD3ReasonForNoCompetition.DProtectRights;
                if (element.Element("D_ARTISTIC") != null)
                    reason = AnnexD3ReasonForNoCompetition.DArtistic;
                if (element.Element("D_EXCLUSIVE_RIGHT") != null)
                    reason = AnnexD3ReasonForNoCompetition.DExistenceExclusive;
                //if (element.Element("D_EXTREME_URGENCY") != null)
                //    reason = ReasonForNoCompetition.Undefined;
            }
            return reason;
        }

        private static AnnexD2ReasonForNoCompetition GetReasonAnnexD2(XElement element)
        {
            AnnexD2ReasonForNoCompetition reason = AnnexD2ReasonForNoCompetition.Undefined;

            if (element != null)
            {
                if (element.Element("D_TECHNICAL") != null)
                    reason = AnnexD2ReasonForNoCompetition.DTechnical;
                if (element.Element("D_PROTECT_RIGHTS") != null)
                    reason = AnnexD2ReasonForNoCompetition.DProtectRights;
                if (element.Element("D_ARTISTIC") != null)
                    reason = AnnexD2ReasonForNoCompetition.DArtistic;
                if (element.Element("D_EXCLUSIVE_RIGHT") != null)
                    reason = AnnexD2ReasonForNoCompetition.DExistenceExclusive;
                //if (element.Element("D_EXTREME_URGENCY") != null)
                //    reason = ReasonForNoCompetition.Undefined;
            }
            return reason;
        }

        private static AnnexD1ReasonForNoCompetition GetReasonAnnexD1(XElement element)
        {
            AnnexD1ReasonForNoCompetition reason = AnnexD1ReasonForNoCompetition.Undefined;

            if (element != null)
            {
                if (element.Element("D_TECHNICAL") != null)
                    reason = AnnexD1ReasonForNoCompetition.DTechnical;
                if (element.Element("D_PROTECT_RIGHTS") != null)
                    reason = AnnexD1ReasonForNoCompetition.DProtectRights;
                if (element.Element("D_ARTISTIC") != null)
                    reason = AnnexD1ReasonForNoCompetition.DArtistic;
                if (element.Element("D_EXCLUSIVE_RIGHT") != null)
                    reason = AnnexD1ReasonForNoCompetition.DExistenceExclusive;
                //if (element.Element("D_EXTREME_URGENCY") != null)
                //    reason = ReasonForNoCompetition.Undefined;
            }
            return reason;
        }

        private static AnnexD4ReasonForNoCompetition GetReasonAnnexD4(XElement element)
        {
            AnnexD4ReasonForNoCompetition reason = AnnexD4ReasonForNoCompetition.Undefined;

            if (element != null)
            {
                if (element.Element("D_TECHNICAL") != null)
                    reason = AnnexD4ReasonForNoCompetition.DTechnical;
                if (element.Element("D_PROTECT_RIGHTS") != null)
                    reason = AnnexD4ReasonForNoCompetition.DProtectRights;
                if (element.Element("D_ARTISTIC") != null)
                    reason = AnnexD4ReasonForNoCompetition.DArtistic;
                if (element.Element("D_EXCLUSIVE_RIGHT") != null)
                    reason = AnnexD4ReasonForNoCompetition.DExistenceExclusive;
                //if (element.Element("D_EXTREME_URGENCY") != null)
                //    reason = ReasonForNoCompetition.Undefined;
            }
            return reason;
        }

        private static Annex ParseDefenceAnnex(XElement procedure)
        {
            var annex = new Annex() { D3 = new AnnexD3() };

            if (procedure != null)
            {
                var annexD = procedure?.Element("ANNEX_D");
                var justificationChoiceElement = annexD?.Element("JUSTIFICATION_CHOICE_NEGOCIATED_PROCEDURE");
                var justificationOther = annexD?.Element("OTHER_JUSTIFICATION");


                var ProcedureInformationProcedureType = annexD?.Descendants()?.First()?.Name?.LocalName;
                switch (ProcedureInformationProcedureType)
                {
                    case "JUSTIFICATION_CHOICE_NEGOCIATED_PROCEDURE":
                        annex.D3.ProcedureType = AnnexD3ProcedureType.DProcNegotiatedPriorCallCompetition;
                        break;
                    case "RESTRICTED_PROCEDURE":
                        annex.D3.ProcedureType =AnnexD3ProcedureType.DProcRestricted;
                        break;
                    case "COMPETITIVE_DIALOGUE":
                        annex.D3.ProcedureType = AnnexD3ProcedureType.DProcCompetitiveDialogue;
                        break;
                    default:
                        annex.D3.ProcedureType = AnnexD3ProcedureType.Undefined;
                        break;
                }

                if (annex.D3.ProcedureType != AnnexD3ProcedureType.Undefined)
                {
                    annex.D3.NoTenders = true;
                }

                annex.D3.Justification = ParsePElements(annexD?.Element("REASON_CONTRACT_LAWFUL"));
                annex.D3.AllTenders = annexD?.Element("ONLY_IRREGULAR_INACCEPTABLE_TENDERERS") != null;



                annex.D3.ReasonForNoCompetition = justificationChoiceElement?.Element("REASONS_PROVIDED_PARTICULAR_TENDERER")?.Element("REASONS_PROVIDED_PARTICULAR_TENDERER_TECHNICAL") != null ?
                                                   AnnexD3ReasonForNoCompetition.DTechnical : justificationChoiceElement?.Element("REASONS_PROVIDED_PARTICULAR_TENDERER")?.Element("REASONS_PROVIDED_PARTICULAR_TENDERER_EXCLUSIVE_RIGHTS") != null ?
                                                   AnnexD3ReasonForNoCompetition.DExistenceExclusive : AnnexD3ReasonForNoCompetition.Undefined;

                annex.D3.ProvidedByOnlyParticularOperator = annex.D3.ReasonForNoCompetition != AnnexD3ReasonForNoCompetition.Undefined;

                annex.D3.ExtremeUrgency = justificationChoiceElement?.Element("EXTREME_URGENCY_EVENTS_UNFORESEEABLE") != null;

                annex.D3.CrisisUrgency = justificationChoiceElement?.Element("PERIOD_FOR_PROCEDURE_INCOMPATIBLE_WITH_CRISIS") != null;

                annex.D3.OtherServices = justificationChoiceElement?.Element("CONTRACT_RESEARCH_DIRECTIVE") != null;

                annex.D3.ProductsManufacturedForResearch = justificationChoiceElement?.Element("MANUFACTURED_BY_DIRECTIVE") != null;

                annex.D3.AdditionalDeliveries = justificationChoiceElement?.Element("ADDITIONAL_WORKS") != null;

                annex.D3.CommodityMarket = justificationChoiceElement?.Element("SUPPLIES_QUOTED_PURCHASED_COMMODITY_MARKET") != null;

                annex.D3.AdvantageousTerms = justificationChoiceElement?.Element("PURCHASE_SUPPLIES_ADVANTAGEOUS_TERMS") != null;

                annex.D3.AdvantageousPurchaseReason = justificationChoiceElement?.Element("RECEIVERS_ARRANGEMENT_CREDITORS") != null ?
                                                      AnnexD3AdvantageousPurchaseReason.DFromReceivers : justificationChoiceElement?.Element("SUPPLIER_WINDING_UP_BUSINESS") != null ?
                                                      AnnexD3AdvantageousPurchaseReason.DFromWindingSupplier : AnnexD3AdvantageousPurchaseReason.Undefined;
                annex.D3.RepetitionExisting = justificationChoiceElement?.Element("WORKS_REPETITION_EXISTING_WORKS") != null;

                annex.D3.MaritimeService = justificationChoiceElement?.Element("AIR_MARITIME_TRANSPORT_FOR_ARMED_FORCES_DEPLOYMENT") != null;

                if (justificationOther != null)
                {

                    annex.D3.OtherJustification = justificationOther?.Element("CONTRACT_SERVICES_LISTED_IN_DIRECTIVE") != null ?
                                                  AnnexD3OtherJustification.ContractServicesListedInDirective :
                                                  AnnexD3OtherJustification.ContractServicesOutsideDirective;
                }



            }
            return annex;
        }

        private static List<Change> ParseChanges(XElement changes)
        {
            if (changes == null)
            {
                return null;
            }

            return changes.Elements("CHANGE").Select(changeElement =>
            {
                var change = new Change()
                {
                    Section = changeElement?.Element("WHERE")?.Element("SECTION")?.Value,
                    Label = changeElement?.Element("WHERE")?.Element("LABEL")?.Value,
                    LotNumber = changeElement?.Element("WHERE")?.Element("LOT_NO")?.Value

                };

                var oldValue = changeElement?.Element("OLD_VALUE");
                var newValue = changeElement?.Element("NEW_VALUE");

                if (oldValue != null && newValue != null)
                {
                    var type = oldValue.Descendants().First().Name.LocalName;
                    switch (type)
                    {
                        case "TEXT":
                            change.OldText = ParsePElements(oldValue?.Element("TEXT"));
                            change.NewText = ParsePElements(newValue?.Element("TEXT"));
                            break;
                        case "DATE":
                            change.OldDate = ParseChangeDateTime(oldValue);
                            change.NewDate = ParseChangeDateTime(newValue);
                            break;
                        case "CPV_ADDITIONAL":
                            change.NewAdditionalCpvCodes = ParseAdditionalCpvCodes(oldValue).ToList();
                            change.OldAdditionalCpvCodes = ParseAdditionalCpvCodes(newValue).ToList();
                            break;
                        case "CPV_MAIN":
                            change.NewMainCpvCode = ParseCpvCode(oldValue);
                            change.OldMainCpvCode = ParseCpvCode(newValue);
                            break;
                    }

                }
                return change;
            }).ToList();


        }

        private static DateTime? ParseChangeDateTime(XElement change)
        {
            DateTime? date = ParseDate(change?.Element("DATE")?.Value);
            string time = change?.Element("TIME")?.Value;

            if (!string.IsNullOrEmpty(time))
            {
                var timeValue = DateTime.ParseExact(time, "H:mm", null);
                date += timeValue.TimeOfDay;
            }

            return date;
        }

        private static ProcedureInformationProcedureType ParseNationalProcedureInformationProcedureTypes(XElement domesticProcedureInformation)
        {
            if (domesticProcedureInformation == null)
            {
                return ProcedureInformationProcedureType.Undefined;
            }
            switch (domesticProcedureInformation?.Elements()?.First()?.Name.LocalName)
            {
                case "OPEN":
                    return ProcedureInformationProcedureType.ProctypeOpen;
                case "RESTRICTED":
                    return ProcedureInformationProcedureType.ProctypeRestricted;
                case "NEGOTIATED":
                    return ProcedureInformationProcedureType.ProctypeNegotiation;
                case "COMPETITIVE_DIALOGUE":
                    return ProcedureInformationProcedureType.ProctypeCompDialogue;
                case "INNOVATION_PARTNERSHIP":
                    return ProcedureInformationProcedureType.ProctypeInnovation;
                case "DESIGN_CONTEST":
                    return ProcedureInformationProcedureType.Undefined;
                case "DPS":
                    return ProcedureInformationProcedureType.Undefined;
                case "OTHER":
                    return ProcedureInformationProcedureType.ProctypeOther;
                case "PT_DIRECT_AWARD":
                    return ProcedureInformationProcedureType.ProctypeNationalDirect;
                default:
                    return ProcedureInformationProcedureType.Undefined;
            }
        }

        private CommunicationInformation ParseNationalCommunicationInformation(XElement domesticAuthorityInformation, ContractBodyContactInformation organisationInformation)
        {
            var sendToTenders = domesticAuthorityInformation?.Element("DOMESTIC_TENDERS_REQUESTS_APPLICATIONS_MUST_BE_SENT_TO") ??
                                domesticAuthorityInformation?.Element("GENERAL_TENDERS_REQUESTS_APPLICATIONS_MUST_BE_SENT_TO");
            var communicationInformation = new CommunicationInformation()
            {
                SendTendersOption = ParseNationalSendTendersOption(sendToTenders),
            };

            communicationInformation.ElectronicAddressToSendTenders = sendToTenders?.Element("URL_DOCUMENT")?.Value;

            switch (communicationInformation.SendTendersOption)
            {
                case CommunicationInformationSendTendersOption.AddressSendTenders:
                    communicationInformation.ElectronicAddressToSendTenders = sendToTenders?.Element("URL_DOCUMENT")?.Value;
                    break;
                case CommunicationInformationSendTendersOption.AddressOrganisation:
                    communicationInformation.AddressToSendTenders = organisationInformation;
                    break;
                case CommunicationInformationSendTendersOption.AddressFollowing:
                    communicationInformation.AddressToSendTenders = ParseContractingBodyInformation(_nutsSchema, sendToTenders);
                    break;
                default:
                    break;
            }

            return communicationInformation;
        }

        private static CommunicationInformationSendTendersOption ParseNationalSendTendersOption(XElement domesticTendersOption)
        {
            string localName = domesticTendersOption?.Elements()?.First()?.Name?.LocalName;
            if (localName == null)
            {
                return CommunicationInformationSendTendersOption.Undefined;
            }
            switch (localName)
            {
                case "IDEM":
                    return CommunicationInformationSendTendersOption.AddressOrganisation;
                case "URL_DOCUMENT":
                    return CommunicationInformationSendTendersOption.AddressSendTenders;
                default:
                    return CommunicationInformationSendTendersOption.AddressFollowing;
            }

        }

        private static ValueRangeContract ParseNationalEstimatedValue(XElement domesticObjectContract)
        {
            var domesticCostRange = domesticObjectContract?.Element("DOMESTIC_COSTS_RANGE_AND_CURRENCY");

            if (domesticCostRange == null)
            {
                domesticCostRange = domesticObjectContract?.Element("QUANTITY_SCOPE_WORKS_DEFENCE")?.Element("COSTS_RANGE_AND_CURRENCY");
            }

            return new ValueRangeContract()
            {
                Currency = domesticCostRange?.Attribute("CURRENCY")?.Value,
                MinValue = ParseDouble(domesticCostRange?.Element("RANGE_VALUE_COST")?.Element("LOW_VALUE")?.Value),
                MaxValue = ParseDouble(domesticCostRange?.Element("RANGE_VALUE_COST")?.Element("HIGH_VALUE")?.Value),
                Type = domesticCostRange?.Element("RANGE_VALUE_COST") != null ? ValueRangeContractType.Range : domesticCostRange?.Element("VALUE_COST") != null ? ValueRangeContractType.Exact : ValueRangeContractType.Undefined,
                DisagreeToBePublished = domesticCostRange?.Attribute("IS_PUBLIC")?.Value == "NO" ? true :
                                     domesticCostRange?.Attribute("IS_PUBLIC")?.Value == "YES" ? false : false,
                Value = ParseDouble(domesticCostRange?.Element("VALUE_COST")?.Value)
            };
        }

        private static NoticeContractType ParseNationalNoticeType(string noticeType)
        {
            switch (noticeType)
            {
                case "REQUEST_FOR_INFORMATION":
                    return NoticeContractType.NationalPriorInformation;
                case "DOMESTIC_CONTRACT":
                    return NoticeContractType.NationalContract;
                case "PROCUREMENT_DISCONTINUED":
                    return NoticeContractType.NationalContract;
                default:
                    return NoticeContractType.Undefined;
            }

        }

        private static DateTime? ParseDateTimeFromElements(XElement timeElement)
        {

            if (timeElement == null)
            {
                return null;
            }

            var year = ParseInt(timeElement?.Element("YEAR")?.Value);
            var month = ParseInt(timeElement?.Element("MONTH")?.Value);
            var day = ParseInt(timeElement?.Element("DAY")?.Value);
            var timeString = timeElement?.Element("TIME")?.Value;
            var date = new DateTime(year, month, day);

            if (!string.IsNullOrEmpty(timeString))
            {
                var time = DateTime.ParseExact(timeString, "H:mm", null);
                date += time.TimeOfDay;
            }

            return date;
        }

        private static IEnumerable<ObjectDescription> ParseNationalObjectDescriptions(XNamespace nutsSchema, XElement objectContract, XElement domesticContractRC, XElement domesticObjectContract)
        {
            var childElement = objectContract?.Descendants("DOMESTIC_CONTRACT_RELATING_CONDITIONS").ToArray().Length > 0 ? objectContract?.Descendants("DOMESTIC_CONTRACT_RELATING_CONDITIONS") : objectContract?.Descendants("GENERAL_CONTRACT_RELATING_CONDITIONS");

            if (objectContract?.Descendants("DOMESTIC_CONTRACT_RELATING_CONDITIONS").ToArray().Length == 0 || objectContract?.Descendants("GENERAL_CONTRACT_RELATING_CONDITIONS").ToArray().Length == 0)
            {
                childElement = objectContract?.Descendants("DOMESTIC_OBJECT_INFORMATION").ToArray().Length > 0 ? objectContract?.Descendants("DOMESTIC_OBJECT_INFORMATION") : objectContract?.Descendants("AGRICULTURE_OBJECT_INFORMATION_TENDER").ToArray().Length > 0 ? objectContract?.Descendants("AGRICULTURE_OBJECT_INFORMATION_TENDER") : objectContract?.Descendants("GENERAL_OBJECT_INFORMATION");
            }
            if (childElement == null)
            {

                return Enumerable.Empty<ObjectDescription>();
            }

            return childElement.Select((objectDescription, index) =>
            {
                var objectDescriptionModel = new ObjectDescription()
                {
                    Title = objectDescription?.Element("TITLE_CONTRACT")?.Value,
                    LotNumber = $"{index + 1}",
                    AwardCriteria = new AwardCriteria()
                    {
                        QualityCriteria = ParseNationalAwardCriteria(domesticContractRC?.Element("AWARD_CRITERIA")?.Element("DOMESTIC_AC_PRICE_QUALITY")?.Element("DOMESTIC_CRITERIA_STATED_BELOW")?.Elements("AC_DEFINITION") ?? domesticContractRC?.Element("AWARD_CRITERIA")?.Element("MOST_ECONOMICALLY_ADVANTAGEOUS_TENDER_SHORT")?.Elements("CRITERIA_DEFINITION")),
                        CriterionTypes = ParseNationalCostAndQualityCriteria(domesticContractRC?.Element("AWARD_CRITERIA")),
                        CostCriteria = domesticContractRC?.Element("AWARD_CRITERIA")?.Element("DOMESTIC_AC_COST") != null ? new[]
                        {
                            new AwardCriterionDefinition()
                            {
                                Criterion = "Edullisimmat kustannukset",
                                Weighting = "100 %"
                            }
                        } : new AwardCriterionDefinition[0]
                    },
                    EstimatedValue = ParseNationalEstimatedValue(domesticObjectContract),
                    AwardContract = ParseNationalAward(domesticObjectContract),
                    MainsiteplaceWorksDelivery = domesticObjectContract?.Element("LOCATION")?.Value != null ? new string[] { domesticObjectContract?.Element("LOCATION")?.Value } : new string[0],
                    TendersMustBePresentedAsElectronicCatalogs = objectDescription.Element("DOMESTIC_ECATALOG_REQUIRED") != null,
                    DescrProcurement = ParsePElements(domesticObjectContract.Element("SHORT_CONTRACT_DESCRIPTION") ?? objectDescription?.Element("DESCRIPTION") ?? domesticObjectContract.Element("SHORT_BUSINESS_DESCRIPTION")),
                    OptionsAndVariants = new OptionsAndVariants()
                    {
                        VariantsWillBeAccepted = objectContract?.Element("DOMESTIC_PROCEDURE_DEFINITION")?.Element("ACCEPTED_VARIANTS") != null ? objectContract?.Element("DOMESTIC_PROCEDURE_DEFINITION")?.Element("ACCEPTED_VARIANTS")?.Attribute("VALUE").Value == "YES" :
                        domesticObjectContract?.Element("ACCEPTED_VARIANTS")?.Attribute("VALUE").Value == "YES"
                    },
                    NutsCodes = ParseNationalNuts(domesticObjectContract, nutsSchema),
                    MainCpvCode = new CpvCode
                    {
                        Code = domesticObjectContract?.Element("CPV")?.Element("CPV_MAIN")?.Element("CPV_CODE")?.Attribute("CODE")?.Value,
                        VocCodes = domesticObjectContract?.Element("CPV")?.Element("CPV_MAIN")?.Elements("CPV_SUPPLEMENTARY_CODE").Select(s => new VocCode { Code = s.Attribute("CODE")?.Value }).ToArray()
                    },
                    AdditionalCpvCodes = ParseAdditionalCpvCodes(domesticObjectContract?.Element("CPV")),
                    EuFunds = new EuFunds() { },
                    TimeFrame = ParseNationalTimeFrame(domesticObjectContract?.Element("PERIOD_WORK_DATE_STARTING")?.Element("INTERVAL_DATE") ?? domesticObjectContract?.Element("PROCEDURE_DATE_STARTING"))
                };
                return objectDescriptionModel;
            });

        }

        private static Award ParseNationalAward(XElement domesticObjectContract)
        {
            var hasContractor = domesticObjectContract?.Element("DOMESTIC_CONTRACTOR") != null;
            return new Award()
            {

                AwardedContract = new ContractAward()
                {
                    Contractors = hasContractor ? new List<ContractorContactInformation>() {
                        new ContractorContactInformation(){
                            OfficialName = domesticObjectContract?.Element("DOMESTIC_CONTRACTOR")?.Element("OFFICIALNAME")?.Value,
                            NationalRegistrationNumber =domesticObjectContract?.Element("DOMESTIC_CONTRACTOR")?.Element("NATIONALID")?.Value,

                        }
                    } : new List<ContractorContactInformation>()
                },



            };
        }

        private static IEnumerable<ObjectDescription> ParseDefenceObjectDescriptions(XNamespace nutsSchema, XElement defenceObjectContract)
        {
            var defenceContract = defenceObjectContract?.Element("F17_DIVISION_INTO_LOTS");

            if (defenceObjectContract == null)
            {
                return null;
            }

            if (defenceObjectContract.Element("QUANTITY_SCOPE_WORKS_DEFENCE") != null)
            {
                return new List<ObjectDescription>(){
                 new ObjectDescription(){
                    AdditionalInformation = ParsePElements(defenceObjectContract?.Element("ADDITIONAL_INFORMATION"))
                    }
                };

            }

            return defenceContract?.Descendants("F17_ANNEX_B").Select((objectDescription, index) =>
            {
                var objectDescriptionModel = new ObjectDescription()
                {

                    LotNumber = objectDescription?.Element("LOT_NUMBER")?.Value,
                    Title = objectDescription?.Element("LOT_TITLE")?.Value,
                    DescrProcurement = ParsePElements(objectDescription.Element("LOT_DESCRIPTION")),
                    MainCpvCode = new CpvCode
                    {
                        Code = objectDescription?.Element("CPV")?.Element("CPV_MAIN").Element("CPV_CODE")?.Attribute("CODE")?.Value,
                        VocCodes = objectDescription?.Element("CPV")?.Element("CPV_MAIN").Elements("CPV_SUPPLEMENTARY_CODE").Select(s => new VocCode { Code = s.Attribute("CODE")?.Value }).ToArray()
                    },
                    AdditionalCpvCodes = ParseAdditionalCpvCodes(objectDescription?.Element("CPV")),
                    EstimatedValue = ParseValueRangeContract(objectDescription),
                    QuantityOrScope = ParsePElements(objectDescription?.Element("NATURE_QUANTITY_SCOPE")?.Element("TOTAL_QUANTITY_OR_SCOPE")),
                    AwardCriteria = new AwardCriteria()
                    {
                        CriterionTypes = ParseNationalCostAndQualityCriteria(objectDescription?.Element("AWARD_CRITERIA")),
                        QualityCriteria = new List<AwardCriterionDefinition>().ToArray(),
                        CostCriteria = new AwardCriterionDefinition[] { }
                    },
                    OptionsAndVariants = new OptionsAndVariants()
                    {
                        VariantsWillBeAccepted = defenceObjectContract?.Element("ACCEPTED_VARIANTS")?.Attribute("VALUE").Value == "YES"
                    },
                    EuFunds = new EuFunds() { }


                };
                return objectDescriptionModel;
            });

        }

        private static string[] ParseNationalNuts(XElement domesticObjectContract, XNamespace nutsSchema)
        {
            return domesticObjectContract.Elements(nutsSchema + "NUTS").Select(nuts => nuts?.Attribute("CODE")?.Value).Where(n => !string.IsNullOrEmpty(n)).ToArray();
        }

        private static AwardCriteriaCriterionTypes ParseNationalCostAndQualityCriteria(XElement awardCriteria)
        {
            if (awardCriteria == null)
            {
                return AwardCriteriaCriterionTypes.Undefined;
            }
            var awardCriteriaType = awardCriteria?.Descendants().FirstOrDefault();
            switch (awardCriteriaType?.Name.LocalName)
            {
                case "DOMESTIC_AC_PRICE_QUALITY":
                case "MOST_ECONOMICALLY_ADVANTAGEOUS_TENDER_SHORT":
                    if (awardCriteriaType.Element("CRITERIA_STATED_IN_OTHER_DOCUMENT") != null)
                    {
                        return AwardCriteriaCriterionTypes.DescriptiveCriteria;
                    }
                    return AwardCriteriaCriterionTypes.PriceAndQualityCriteria;
                case "LOWEST_PRICE":
                    return AwardCriteriaCriterionTypes.PriceCriterion;
                case "DOMESTIC_AC_COST":
                    return AwardCriteriaCriterionTypes.CostCriterion;

                default:
                    return AwardCriteriaCriterionTypes.Undefined;
            }

        }

        private static AwardCriterionDefinition[] ParseNationalAwardCriteria(IEnumerable<XElement> acDefinitions)
        {
            return acDefinitions?.Select(ac => new AwardCriterionDefinition()
            {
                Criterion = ac.Element("AC_CRITERION")?.Value != null ? ac.Element("AC_CRITERION")?.Value : ac.Element("CRITERIA")?.Value,
                Weighting = ac.Element("AC_WEIGHTING")?.Value != null ? ac.Element("AC_WEIGHTING")?.Value : ac.Element("WEIGHTING")?.Value
            }).ToArray() ?? new AwardCriterionDefinition[0];
        }

        private static TedPublicationInfo ParseTedPublicationInfo(INoticeImportModel importedNotice)
        {
            return new TedPublicationInfo()
            {
                Publication_date = importedNotice.TedPublishedDate.GetValueOrDefault(),
                No_doc_ojs = importedNotice.NoticeOjsNumber,
                Ojs_number = importedNotice.NoticeOjsNumber,
                Ted_links = new TedLinks()
            };
        }

        private static ProceduresForReviewInformation ParseProceduresForReview(XElement reviewBodyAddress, XElement complementaryInfo)
        {
            if (reviewBodyAddress == null)
            {
                return new ProceduresForReviewInformation() { };
            }
            return new ProceduresForReviewInformation()
            {
                ReviewBody = new ContractBodyContactInformation()
                {
                    OfficialName = reviewBodyAddress?.Element("OFFICIALNAME")?.Value != null ? reviewBodyAddress?.Element("OFFICIALNAME")?.Value :
                    reviewBodyAddress?.Element("ORGANISATION")?.Element("OFFICIALNAME")?.Value,
                    PostalAddress = ParsePostalAddress(reviewBodyAddress),
                    Email = reviewBodyAddress.Element("E_MAIL")?.Value != null ?
                            reviewBodyAddress.Element("E_MAIL")?.Value :
                            reviewBodyAddress.Element("E_MAILS")?.Element("E_MAIL")?.Value != null ?
                            reviewBodyAddress.Element("E_MAILS")?.Element("E_MAIL")?.Value : "",
                    MainUrl = reviewBodyAddress?.Element("URL")?.Value,
                    TelephoneNumber = reviewBodyAddress?.Element("PHONE")?.Value,
                },
                ReviewProcedure = ParsePElements(complementaryInfo?.Element("REVIEW_PROCEDURE"), 0),
                
            };
        }

        private static LotsInfo ParseLotsInfo(XElement objectContract)
        {
            var lotDivisionElement = objectContract.Element("LOT_DIVISION");
            var hasLotDivision = lotDivisionElement != null;

            if (!hasLotDivision)
            {
                return new LotsInfo()
                {
                    DivisionLots = false,
                    
                };
            }

            var lotsInfo = new LotsInfo()
            {
                DivisionLots = lotDivisionElement != null,
                LotCombinationPossible = lotDivisionElement?.Element("LOT_COMBINING_CONTRACT_RIGHT") != null,
                LotCombinationPossibleDescription = ParsePElements(lotDivisionElement?.Element("LOT_COMBINING_CONTRACT_RIGHT"), 0),
                LotsMaxAwarded = lotDivisionElement?.Element("LOT_MAX_ONE_TENDERER") != null,
                LotsMaxAwardedQuantity = ParseInt(lotDivisionElement?.Element("LOT_MAX_ONE_TENDERER")?.Value ?? "0"),
                QuantityOfLots = objectContract.Elements("OBJECT_DESCR")?.Count() ?? 1,
                LotsSubmittedFor = ParseLotsSubmittedFor(objectContract),
                LotsSubmittedForQuantity = ParseInt(objectContract.Element("LOT_MAX_ONE_TENDERER")?.Value),
                
            };


            if (lotsInfo.LotsSubmittedFor == LotsInfoLotsSubmittedFor.LotsMax)
            {
                lotsInfo.LotsSubmittedForQuantity = ParseInt(lotDivisionElement?.Element("LOT_MAX_NUMBER")?.Value);
            }
            return lotsInfo;
        }

        private static LotsInfoLotsSubmittedFor ParseLotsSubmittedFor(XElement objectContract)
        {
            return objectContract.Element("LOT_DIVISION")?.Element("LOT_ALL") != null ? LotsInfoLotsSubmittedFor.LotsAll :
                   objectContract.Element("LOT_DIVISION")?.Element("LOT_MAX_NUMBER") != null ? LotsInfoLotsSubmittedFor.LotsMax :
                   objectContract.Element("LOT_DIVISION")?.Element("LOT_ONE_ONLY") != null ? LotsInfoLotsSubmittedFor.LotOneOnly :
                   LotsInfoLotsSubmittedFor.Undefined;
        }

        private static CommunicationInformation ParseCommunicationInformation(XNamespace nutsSchema, XElement contractingBody)
        {
            return new CommunicationInformation()
            {
                AdditionalInformation = contractingBody?.Element("ADDRESS_FURTHER_INFO_IDEM") != null ? CommunicationInformationAdditionalInformation.AddressToAbove : contractingBody?.Element("ADDRESS_FURTHER_INFO") != null ? CommunicationInformationAdditionalInformation.AddressAnother : CommunicationInformationAdditionalInformation.Undefined,
                AdditionalInformationAddress = contractingBody?.Element("ADDRESS_FURTHER_INFO") != null ? ParseContractingBodyInformation(nutsSchema, contractingBody?.Element("ADDRESS_FURTHER_INFO")) : null,
                ProcurementDocumentsAvailable = contractingBody?.Element("DOCUMENT_FULL") != null ? CommunicationInformationProcurementDocumentsAvailable.AddressObtainDocs : contractingBody?.Element("DOCUMENT_RESTRICTED") != null ? CommunicationInformationProcurementDocumentsAvailable.DocsRestricted : CommunicationInformationProcurementDocumentsAvailable.Undefined,
                ProcurementDocumentsUrl = contractingBody?.Element("URL_DOCUMENT")?.Value,
                ElectronicAddressToSendTenders = contractingBody?.Element("URL_PARTICIPATION")?.Value,
                AddressToSendTenders = contractingBody?.Element("ADDRESS_PARTICIPATION") != null ? ParseContractingBodyInformation(nutsSchema, contractingBody?.Element("ADDRESS_PARTICIPATION")) : null,

                ElectronicCommunicationInfoUrl = contractingBody.Element("URL_TOOL")?.Value,
                
                SendTendersOption = contractingBody?.Element("URL_PARTICIPATION") != null ? CommunicationInformationSendTendersOption.AddressSendTenders :
                                    contractingBody?.Element("ADDRESS_PARTICIPATION") != null ? CommunicationInformationSendTendersOption.AddressFollowing :
                                    contractingBody?.Element("ADDRESS_PARTICIPATION_IDEM") != null ? CommunicationInformationSendTendersOption.AddressOrganisation :
                                    CommunicationInformationSendTendersOption.Undefined
            };

        }

        private static CommunicationInformation ParseDefenceCommunicationInformation(XNamespace nutsSchema, XElement contractingBody, NoticeContractType noticeType)
        {
            var prefix1 = noticeType == NoticeContractType.DefenceContractAward ? "_AWARD" : "";

            var nameAddressesContatContract = contractingBody?.Element($"NAME_ADDRESSES_CONTACT_CONTRACT{prefix1}") ?? contractingBody?.Element("NAME_ADDRESSES_CONTACT_PRIOR_INFORMATION");
            var specAndAdditionalDocs = nameAddressesContatContract?.Element("SPECIFICATIONS_AND_ADDITIONAL_DOCUMENTS");
            var internetAddresses = nameAddressesContatContract?.Element($"INTERNET_ADDRESSES_CONTRACT{prefix1}") ?? nameAddressesContatContract?.Element("INTERNET_ADDRESSES_PRIOR_INFORMATION");
            var addressToSendTenders = nameAddressesContatContract?.Element("TENDERS_REQUESTS_APPLICATIONS_MUST_BE_SENT_TO");
            var communicationInformation = new CommunicationInformation()
            {
                AdditionalInformation = nameAddressesContatContract?.Element("FURTHER_INFORMATION")?.Element("IDEM") != null ?
                                        CommunicationInformationAdditionalInformation.AddressToAbove : contractingBody?.Element("ADDRESS_FURTHER_INFO") != null ?
                                        CommunicationInformationAdditionalInformation.AddressAnother : CommunicationInformationAdditionalInformation.Undefined,

                AdditionalInformationAddress = ParseContractingBodyInformation(nutsSchema, nameAddressesContatContract?.Element("FURTHER_INFORMATION")?.Element("CONTACT_DATA")),

                ElectronicAccess = internetAddresses?.Element("URL_INFORMATION")?.Value,

                SendTendersOption = internetAddresses?.Element("URL_PARTICIPATE") != null && addressToSendTenders == null ?
                                    CommunicationInformationSendTendersOption.AddressSendTenders :
                                    addressToSendTenders == null ?
                                    CommunicationInformationSendTendersOption.Undefined :
                                    addressToSendTenders?.Element("IDEM") != null ?
                                    CommunicationInformationSendTendersOption.AddressOrganisation :
                                    CommunicationInformationSendTendersOption.AddressFollowing,
                ElectronicAddressToSendTenders = internetAddresses?.Element("URL_PARTICIPATE")?.Value,
                AddressToSendTenders = ParseContractingBodyInformation(nutsSchema, addressToSendTenders),
                

            };

            if (specAndAdditionalDocs?.Element("IDEM") != null)
            {
                communicationInformation.SpecsAndAdditionalDocuments = CommunicationInformationSpecsAndAdditionalDocuments.AddressToAbove;

            }
            else if (specAndAdditionalDocs?.Element("CONTACT_DATA") != null)
            {
                communicationInformation.SpecsAndAdditionalDocuments = CommunicationInformationSpecsAndAdditionalDocuments.AddressAnother;
                communicationInformation.SpecsAndAdditionalDocumentsAddress = ParseContractingBodyInformation(nutsSchema, specAndAdditionalDocs.Element("CONTACT_DATA"));
            }

            return communicationInformation;

        }

        private static ConditionsInformation ParseConditionsInformation(XElement lefti)
        {
            if (lefti == null)
            {
                return new ConditionsInformation();
            }
            var conditionsInformation = new ConditionsInformation()
            {
                ProfessionalSuitabilityRequirements = ParsePElements(lefti?.Element("SUITABILITY"), 0),
                TechnicalCriteriaToParticipate = lefti?.Element("TECHNICAL_CRITERIA_DOC") != null ? true : false,
                EconomicCriteriaDescription = ParsePElements(lefti?.Element("ECONOMIC_FINANCIAL_INFO"), 0),
                EconomicRequiredStandards = ParsePElements(lefti?.Element("ECONOMIC_FINANCIAL_MIN_LEVEL"), 0),
                TechnicalCriteriaDescription = ParsePElements(lefti?.Element("TECHNICAL_PROFESSIONAL_INFO"), 0),
                TechnicalRequiredStandards = ParsePElements(lefti?.Element("TECHNICAL_PROFESSIONAL_MIN_LEVEL"), 0),
                
                ReferenceToRelevantLawRegulationOrProvision = ParsePElements(lefti?.Element("REFERENCE_TO_LAW"), 0),
                ExecutionOfServiceIsReservedForProfession = lefti?.Element("PARTICULAR_PROFESSION") != null,
                ObligationToIndicateNamesAndProfessionalQualifications = lefti?.Element("PERFORMANCE_STAFF_QUALIFICATION") != null,

                CiriteriaForTheSelectionOfParticipants = ParsePElements(lefti.Element("CRITERIA_SELECTION")),
                EconomicCriteriaToParticipate = lefti?.Element("ECONOMIC_CRITERIA_DOC") != null ? true : false,
                ParticipationIsReservedForProfession = lefti?.Element("PARTICULAR_PROFESSION") != null ? true : false,
                DepositsRequired = ParsePElements(lefti?.Element("DEPOSIT_GUARANTEE_REQUIRED")),
                FinancingConditions = ParsePElements(lefti?.Element("MAIN_FINANCING_CONDITION")),
                LegalFormTaken = ParsePElements(lefti?.Element("LEGAL_FORM")),
                ContractPerformanceConditions = ParsePElements(lefti?.Element("PERFORMANCE_CONDITIONS")),
                RestrictedToShelteredProgram = lefti?.Element("RESTRICTED_SHELTERED_PROGRAM") != null,
                RulesForParticipation = ParsePElements(lefti?.Element("RULES_CRITERIA")),
                RestrictedToShelteredWorkshop = lefti?.Element("RESTRICTED_SHELTERED_WORKSHOP") != null,
                ReservedOrganisationServiceMission = lefti?.Element("RESERVED_ORGANISATIONS_SERVICE_MISSION") != null
            };

            return conditionsInformation;
        }

        private TenderingInformation ParseTenderingInformation(XElement objectContract, XElement procedure)
        {
            var tenderingInformation = new TenderingInformation();

            if (objectContract == null && procedure == null)
            {
                return null;
            }

            if (objectContract != null)
            {
                tenderingInformation.EstimatedDateOfContractNoticePublication = ParseDate(objectContract.Element("DATE_PUBLICATION_NOTICE")?.Value);
            }
            if (procedure != null)
            {
                tenderingInformation.TendersOrRequestsToParticipateDueDateTime = BuildDateTime(procedure?.Element("DATE_RECEIPT_TENDERS")?.Value, procedure?.Element("TIME_RECEIPT_TENDERS")?.Value);
                tenderingInformation.Languages = ParseLanguages(procedure);
                tenderingInformation.TendersMustBeValidForMonths = ParseInt(procedure.Element("DURATION_TENDER_VALID")?.Value ?? null);
                tenderingInformation.TendersMustBeValidOption = procedure.Element("DATE_TENDER_VALID") != null ? TenderingInformationTendersMustBeValidOption.Date :
                                           procedure.Element("DURATION_TENDER_VALID") != null ? TenderingInformationTendersMustBeValidOption.Months :
                                           TenderingInformationTendersMustBeValidOption.TimeNotSet;
                tenderingInformation.TendersMustBeValidUntil = ParseDate(procedure.Element("DATE_TENDER_VALID")?.Value);
                tenderingInformation.TenderOpeningConditions = new TenderOpeningConditions()
                {
                    OpeningDateAndTime = BuildDateTime(
                                            procedure.Element("OPENING_CONDITION")?.Element("DATE_OPENING_TENDERS").Value,
                                            procedure.Element("OPENING_CONDITION")?.Element("TIME_OPENING_TENDERS").Value
                                         ),
                    InformationAboutAuthorisedPersons = ParsePElements(procedure.Element("OPENING_CONDITION")?.Element("INFO_ADD"), 0),
                    Place = ParsePElements(procedure.Element("OPENING_CONDITION")?.Element("PLACE"), 0)
                };
                tenderingInformation.TendersMustBeValidUntil = ParseDate(procedure.Element("DATE_TENDER_VALID")?.Value);
                tenderingInformation.TendersMustBeValidOption = procedure.Element("DATE_TENDER_VALID") != null ? TenderingInformationTendersMustBeValidOption.Date : procedure.Element("DURATION_TENDER_VALID")?.Attribute("TYPE").Value == "MONTH" ? TenderingInformationTendersMustBeValidOption.Months : TenderingInformationTendersMustBeValidOption.TimeNotSet;
            }

            return tenderingInformation;
        }

        private static string[] ParseLanguages(XElement procedure)
        {
            var languages = new List<string>();

            if (procedure?.Element("LANGUAGES") != null)
            {
                foreach (var item in procedure.Element("LANGUAGES")?.Elements("LANGUAGE"))
                {
                    languages.Add(item.Attribute("VALUE")?.Value);
                }
            }
            if (procedure?.Element("LANGUAGE") != null)
            {
                foreach (var lang in procedure.Element("LANGUAGE")?.Elements("LANGUAGE_EC"))
                {
                    languages.Add(lang.Attribute("VALUE")?.Value);
                }
            }

            return languages.ToArray();
        }

        public static DateTime? BuildDateTime(string dateString, string timeString)
        {
            if (string.IsNullOrEmpty(dateString))
            {
                return null;
            }

            var date = ParseDate(dateString);

            if (!string.IsNullOrEmpty(timeString))
            {
                var time = DateTime.ParseExact(timeString, "H:mm", null);
                date += time.TimeOfDay;
            }

            return date;
        }

        private static ProcedureInformation ParseProcedureInformation(XElement procedure)
        {
            if (procedure == null)
            {
                return null;
            }
            return new ProcedureInformation()
            {
                ProcedureType = ParseProcedureInformationProcedureType(procedure),
                ProcurementGovernedByGPA = procedure?.Element("CONTRACT_COVERED_GPA") != null ? true : false,
                FrameworkAgreement = ParseFrameworkAgreementInformation(procedure),
                AcceleratedProcedure = procedure?.Element("ACCELERATED_PROC") != null,
                JustificationForAcceleratedProcedure = ParsePElements(procedure?.Element("ACCELERATED_PROC"), 0),
                UrlNationalProcedure = procedure?.Element("URL_NATIONAL_PROCEDURE")?.Value,
                MainFeaturesAward = ParsePElements(procedure?.Element("MAIN_FEATURES_AWARD")),
                
            };
        }

        private static ProcedureInformationProcedureType ParseProcedureInformationProcedureType(XElement procedure)
        {

            if (procedure?.Element("PT_OPEN") != null) return ProcedureInformationProcedureType.ProctypeOpen;
            if (procedure?.Element("PT_RESTRICTED") != null) return ProcedureInformationProcedureType.ProctypeRestricted;
            if (procedure?.Element("PT_COMPETITIVE_NEGOTIATION") != null) return ProcedureInformationProcedureType.ProctypeCompNegotiation;
            if (procedure?.Element("PT_COMPETITIVE_DIALOGUE") != null) return ProcedureInformationProcedureType.ProctypeCompDialogue;
            if (procedure?.Element("PT_INNOVATION_PARTNERSHIP") != null) return ProcedureInformationProcedureType.ProctypeInnovation;
            if (procedure?.Element("PT_INVOLVING_NEGOTIATION") != null) return ProcedureInformationProcedureType.ProctypeNegotiationsInvolved;
            if (procedure?.Element("PT_NEGOTIATED_WITH_PRIOR_CALL") != null) return ProcedureInformationProcedureType.ProctypeNegotWCall;
            if (procedure?.Element("DIRECTIVE_2014_24_EU")?.Element("PT_NEGOTIATED_WITHOUT_PUBLICATION") != null) return ProcedureInformationProcedureType.AwardWoPriorPubD1;
            if (procedure?.Element("DIRECTIVE_2014_25_EU")?.Element("PT_NEGOTIATED_WITHOUT_PUBLICATION") != null) return ProcedureInformationProcedureType.AwardWoPriorPubD1;
            if (procedure?.Element("DIRECTIVE_2009_81_EC")?.Element("PT_NEGOTIATED_WITHOUT_PUBLICATION") != null) return ProcedureInformationProcedureType.ProctypeNegotiatedWoNotice;
            if (procedure?.Element("PT_AWARD_CONTRACT_WITHOUT_CALL") != null || procedure?.Element("DIRECTIVE_2014_24_EU")?.Element("PT_AWARD_CONTRACT_WITHOUT_CALL") != null)
            {
                if (procedure?.Element("PT_AWARD_CONTRACT_WITHOUT_CALL")?.Element("D_OUTSIDE_SCOPE") != null) return ProcedureInformationProcedureType.ProctypeNegotiatedWoNotice;
                if (procedure?.Element("DIRECTIVE_2014_24_EU")?.Element("PT_AWARD_CONTRACT_WITHOUT_CALL")?.Element("D_OUTSIDE_SCOPE") != null) return ProcedureInformationProcedureType.ProctypeNegotiatedWoNotice;
                return ProcedureInformationProcedureType.AwardWoPriorPubD1;
            }
            if (procedure?.Element("PT_ACCELERATED_RESTRICTED_CHOICE") != null) return ProcedureInformationProcedureType.ProctypeRestricted;
            if (procedure?.Element("PT_ACCELERATED_RESTRICTED") != null) return ProcedureInformationProcedureType.ProctypeRestricted;

            if (procedure?.Element("PT_AWARD_CONTRACT_WITH_PRIOR_PUBLICATION") != null) return ProcedureInformationProcedureType.ProctypeWithConcessNotice;
            if (procedure?.Element("PT_AWARD_CONTRACT_WITHOUT_PUBLICATION") != null)
            {
                return procedure?.Element("PT_AWARD_CONTRACT_WITHOUT_PUBLICATION")?.Element("D_ACCORDANCE_ARTICLE") != null ? ProcedureInformationProcedureType.AwardWoPriorPubD4 : ProcedureInformationProcedureType.AwardWoPriorPubD4Other;
            }

            if (procedure?.Element("F18_PT_NEGOTIATED_WITHOUT_PUBLICATION_CONTRACT_NOTICE")?.Element("ANNEX_D")?.Element("JUSTIFICATION_CHOICE_NEGOCIATED_PROCEDURE") != null) return ProcedureInformationProcedureType.AwardWoPriorPubD1;
            if (procedure?.Element("F18_PT_NEGOTIATED_WITHOUT_PUBLICATION_CONTRACT_NOTICE")?.Element("ANNEX_D")?.Element("OTHER_JUSTIFICATION") != null) return ProcedureInformationProcedureType.AwardWoPriorPubD1Other;

            return ProcedureInformationProcedureType.Undefined;
        }

        private static FrameworkAgreementInformation ParseFrameworkAgreementInformation(XElement procedure)
        {
            var framework = procedure?.Element("FRAMEWORK");
            return new FrameworkAgreementInformation()
            {
                IncludesFrameworkAgreement = framework != null,
                FrameworkAgreementType = framework?.Element("SINGLE_OPERATOR") != null ? FrameworkAgreementInformationFrameworkAgreementType.FrameworkSingle :
                                         framework?.Element("SEVERAL_OPERATORS") != null ? FrameworkAgreementInformationFrameworkAgreementType.FrameworkSeveral :
                                         FrameworkAgreementInformationFrameworkAgreementType.Undefined,
                EnvisagedNumberOfParticipants = framework?.Element("NB_PARTICIPANTS") != null ?
                                                ParseInt(framework?.Element("NB_PARTICIPANTS")?.Value) : new int?(),
                JustificationForDurationOverFourYears = ParsePElements(framework?.Element("JUSTIFICATION")),
                DynamicPurchasingSystemWasTerminated = procedure?.Element("TERMINATION_DPS") != null,
                IncludesDynamicPurchasingSystem = procedure?.Element("DPS") != null

            };
        }

        private static ProcurementObject ParseProcurementObject(XElement objectContract)
        {
            return new ProcurementObject
            {
                MainCpvCode = new CpvCode
                {
                    Code = objectContract.Element("CPV_MAIN")?.Element("CPV_CODE")?.Attribute("CODE")?.Value,
                    VocCodes = objectContract.Element("CPV_MAIN")?.Elements("CPV_SUPPLEMENTARY_CODE").Select(s => new VocCode { Code = s.Attribute("CODE")?.Value }).ToArray()
                },
                ShortDescription = ParsePElements(objectContract.Element("SHORT_DESCR"), 4000),
                
                EstimatedValue = ParseValueRangeContract(objectContract.Element("VAL_ESTIMATED_TOTAL"), null),
                TotalValue = ParseValueRangeContract(objectContract.Element("VAL_TOTAL"), objectContract.Element("VAL_RANGE_TOTAL"))
            };
        }

        private static decimal? ParseDecimal(string value)
        {

            var numberFormatInfo = new NumberFormatInfo { NumberDecimalSeparator = "." };
            return decimal.TryParse(value, NumberStyles.Float, numberFormatInfo, out decimal result) ? result : new decimal?();
        }

        private static double? ParseDouble(string value)
        {

            var numberFormatInfo = new NumberFormatInfo { NumberDecimalSeparator = "." };
            return double.TryParse(value, NumberStyles.Float, numberFormatInfo, out double result) ? result : new double?();
        }

        private static CpvCode ParseCpvCode(XElement objectContract)
        {
            return new CpvCode
            {
                Code = objectContract?.Element("CPV_MAIN")?.Element("CPV_CODE")?.Attribute("CODE")?.Value
            };
        }

        private static ContactPerson ParseContactPerson(XElement addressContractinBody)
        {
            if (addressContractinBody == null)
            {
                return null;
            }
            return new ContactPerson()
            {
                Email = addressContractinBody.Element("E_MAIL")?.Value != null ?
                        addressContractinBody.Element("E_MAIL")?.Value :
                        addressContractinBody?.Element("E_MAILS")?.Element("E_MAIL")?.Value,
                Name = addressContractinBody.Element("CONTACT_POINT")?.Value ?? addressContractinBody.Element("ATTENTION")?.Value,
                Phone = addressContractinBody.Element("PHONE")?.Value
            };
        }

        private static int ParseInt(string value)
        {
            int.TryParse(value, out int parsedValue);
            return parsedValue;
        }

        private static IEnumerable<ObjectDescription> ParseObjectDescription(XNamespace nutsSchema, IEnumerable<XElement> objectDescriptions, IEnumerable<XElement> awardContracts, bool? hasLots)
        {
            var objectDescriptionList = new List<ObjectDescription>();
            var dummyobjectDescriptionList = new List<ObjectDescription>();
            var awardList = new List<Award>();

            var awardData = new
            {
                id = string.Empty,
                award = new Award()
            };


            foreach (XElement objectDescription in objectDescriptions)
            {
                ObjectDescription objectDescriptionModel = new ObjectDescription()
                {
                    AdditionalCpvCodes = ParseAdditionalCpvCodes(objectDescription),
                    Title = objectDescription.Element("TITLE")?.Element("P")?.Value,
                    LotNumber = objectDescription.Element("LOT_NO")?.Value ?? objectDescription.Attribute("ITEM")?.Value ?? null,
                    MainsiteplaceWorksDelivery = ParsePElements(objectDescription.Element("MAIN_SITE"), 0),
                    NutsCodes = ParseNutsCodes(nutsSchema, objectDescription),
                    DescrProcurement = ParsePElements(objectDescription?.Element("SHORT_DESCR"), 4000),
                    AwardCriteria = ParseCriterionTypes(objectDescription?.Element("AC") != null ? objectDescription?.Element("AC") : objectDescription?.Element("DIRECTIVE_2014_24_EU")?.Element("AC") != null ? objectDescription?.Element("DIRECTIVE_2014_24_EU")?.Element("AC") :
                                                        objectDescription?.Element("DIRECTIVE_2014_25_EU")?.Element("AC")),
                    EuFunds = ParseEuFunds(objectDescription),
                    TimeFrame = ParseTimeFrame(objectDescription),
                    OptionsAndVariants = ParseOptionsAndVariants(objectDescription),
                    TendersMustBePresentedAsElectronicCatalogs = objectDescription.Element("ECATALOGUE_REQUIRED") != null,
                    AdditionalInformation = ParsePElements(objectDescription.Element("INFO_ADD"), 0),
                    
                    EstimatedValue = ParseValueRangeContract(objectDescription.Element("VAL_OBJECT"), null),
                    AwardContract = new Award(),
                    DisagreeAwardCriteriaToBePublished = objectDescription.Element("DIRECTIVE_2014_25_EU")?.Element("AC")?.Attribute("PUBLICATION")?.Value == "NO",
                    CandidateNumberRestrictions = new CandidateNumberRestrictions()
                    {
                        Selected = objectDescription.Element("NB_ENVISAGED_CANDIDATE") != null ?
                                   CandidateNumberRestrictionsSelected.EnvisagedNumber :
                                   objectDescription.Element("NB_MAX_LIMIT_CANDIDATE") != null || objectDescription.Element("NB_MIN_LIMIT_CANDIDATE") != null ?
                                   CandidateNumberRestrictionsSelected.Range :
                                   CandidateNumberRestrictionsSelected.Undefined,
                        EnvisagedMaximumNumber = ParseInt(objectDescription.Element("NB_MAX_LIMIT_CANDIDATE")?.Value),
                        EnvisagedMinimumNumber = ParseInt(objectDescription.Element("NB_MIN_LIMIT_CANDIDATE")?.Value),
                        EnvisagedNumber = ParseInt(objectDescription.Element("NB_ENVISAGED_CANDIDATE")?.Value),
                        ObjectiveCriteriaForChoosing = ParsePElements(objectDescription.Element("CRITERIA_CANDIDATE"), 0)
                    },

                };

                objectDescriptionList.Add(objectDescriptionModel);
            }


            foreach (var award in awardContracts.Select((award, item) => (award, item)))
            {
                var awardElement = award.award;
                var awardContract = ParseAwardContract(award.award, nutsSchema);
                var lotNumber = awardElement.Element("LOT_NO")?.Value;
                var title = awardElement.Element("TITLE")?.Value;
                var itemId = award.item;
                var objectDescription = ResolveObjectDescriptionForAward(objectDescriptionList, title, lotNumber);
                if (objectDescription == null)
                {
                    throw new Exception("No ObjectDescription was found matching the award");

                    //var dummyLotNumber = objectDescription?.LotNumber ?? (!string.IsNullOrEmpty(lotNumber) ? lotNumber : itemId.ToString());
                    //var dummyobjectDescription = new ObjectDescription()
                    //{
                    //    LotNumber = dummyLotNumber,
                    //    Title = objectDescription?.Title ?? title,m
                    //    AdditionalCpvCodes = new CpvCode[0],
                    //    NutsCodes = new string[0],
                    //    AwardCriteria = new AwardCriteria()
                    //    {
                    //        CriterionTypes = AwardCriteriaCriterionTypes.Undefined
                    //    }
                    //};
                    //dummyobjectDescription.AwardContract = awardContract;
                    //dummyobjectDescriptionList.Add(dummyobjectDescription);
                }
                else
                {

                    objectDescription.AwardContract = awardContract;
                }
            }
            var result = objectDescriptionList.Union(dummyobjectDescriptionList);
            return result;

        }

        private static ObjectDescription ResolveObjectDescriptionForAward(List<ObjectDescription> objectDescriptionList, string title, string lotNumber)
        {
            if (objectDescriptionList.Count == 1)
            {
                return objectDescriptionList.FirstOrDefault();
            }

            var objectDescription = objectDescriptionList.FirstOrDefault(o => !string.IsNullOrEmpty(lotNumber) && o.LotNumber == lotNumber) ??
                                    objectDescriptionList.FirstOrDefault(o => !string.IsNullOrEmpty(title) && o.Title == title);

            return objectDescription;
        }

        private static Award ParseAwardContract(XElement awardContract, XNamespace nutsSchema)
        {

            var awardedContract = awardContract.Element("AWARDED_CONTRACT");
            var noAwardedContract = awardContract.Element("NO_AWARDED_CONTRACT");
            var award = new Award()
            {
                AwardedContract = ParseAwardedContract(awardedContract),
                ContractAwarded = ParseContractAwardedType(awardContract),
                NoAwardedContract = ParseNoAwardedContract(noAwardedContract)
            };

            if (awardedContract?.Element("CONTRACTORS") != null)
            {
                foreach (var item in awardedContract?.Element("CONTRACTORS")?.Elements("CONTRACTOR"))
                {
                    var addressContractor = item.Element("ADDRESS_CONTRACTOR");
                    award.AwardedContract.Contractors.Add(new ContractorContactInformation()
                    {

                        OfficialName = addressContractor.Element("OFFICIALNAME")?.Value,
                        NationalRegistrationNumber = addressContractor.Element("NATIONALID")?.Value,
                        PostalAddress = ParsePostalAddress(addressContractor),
                        NutsCodes = ParseNutsCodes(nutsSchema, addressContractor),
                        IsSmallMediumEnterprise = item.Element("SME") != null ? true : false,
                        MainUrl = addressContractor.Element("URL")?.Value,
                        Email = addressContractor.Element("E_MAIL")?.Value,
                        TelephoneNumber = addressContractor.Element("PHONE")?.Value,
                        
                    });

                }

            }

            return award;

        }

        private static AwardContractAwarded ParseContractAwardedType(XElement awardedContract)
        {
            return awardedContract.Element("NO_AWARDED_CONTRACT") != null ? AwardContractAwarded.NoAwardedContract :
                   awardedContract.Element("AWARDED_CONTRACT") != null ? AwardContractAwarded.AwardedContract :
                   AwardContractAwarded.Undefined;
        }

        private static ContractAward ParseAwardedContract(XElement awardedContract)
        {
            var countryOrigin = awardedContract?.Element("COUNTRY_ORIGIN")?.Element("NON_COMMUNITY_ORIGIN")?.Attribute("VALUE").Value;
            var contractAward = new ContractAward()
            {
                ContractTitle = awardedContract?.Parent?.Element("TITLE")?.Value,
                ContractNumber = awardedContract?.Element("CONTRACT_NO")?.Value ?? awardedContract?.Parent?.Element("CONTRACT_NO")?.Value,
                LikelyToBeSubcontracted = awardedContract?.Element("LIKELY_SUBCONTRACTED") != null ? true : false,
                ConclusionDate = ParseDate(awardedContract?.Element("DATE_CONCLUSION_CONTRACT")?.Value),

                ValueOfSubcontract = ParseValueContract(awardedContract?.Element("VAL_SUBCONTRACTING")),
                ConcessionRevenue = ParseValueContract(awardedContract?.Element("VALUES")?.Element("VAL_REVENUE")),
                FinalTotalValue = new ValueRangeContract()
                {
                    Currency = awardedContract?.Element("VALUES")?.Element("VAL_RANGE_TOTAL")?.Attribute("CURRENCY")?.Value != null ?
                               awardedContract?.Element("VALUES")?.Element("VAL_RANGE_TOTAL")?.Attribute("CURRENCY")?.Value :
                               awardedContract?.Element("VALUES")?.Element("VAL_TOTAL")?.Attribute("CURRENCY")?.Value,
                    Value = ParseDouble(awardedContract?.Element("VALUES")?.Element("VAL_TOTAL")?.Value),
                    MaxValue = ParseDouble(awardedContract?.Element("VALUES")?.Element("VAL_RANGE_TOTAL")?.Element("HIGH")?.Value),
                    MinValue = ParseDouble(awardedContract?.Element("VALUES")?.Element("VAL_RANGE_TOTAL")?.Element("LOW")?.Value),
                    Type = ParseContractValueType(awardedContract),
                    DisagreeToBePublished = awardedContract?.Element("VALUES")?.Attribute("PUBLICATION")?.Value == "NO",

                },
                InitialEstimatedValueOfContract = new ValueContract
                {
                    Value = ParseDouble(awardedContract?.Element("VALUES")?.Element("VAL_ESTIMATED_TOTAL")?.Value),
                    Currency = awardedContract?.Element("VALUES")?.Element("VAL_ESTIMATED_TOTAL")?.Attribute("CURRENCY")?.Value
                },
                NotPublicFields = new ContractAwardNotPublicFields()
                {
                    CommunityOrigin = awardedContract?.Element("COUNTRY_ORIGIN")?.Element("COMMUNITY_ORIGIN") != null ? true : false,
                    Countries = string.IsNullOrEmpty(countryOrigin) ? new[] { countryOrigin } : new string[0],
                    NonCommunityOrigin = awardedContract?.Element("COUNTRY_ORIGIN")?.Element("NON_COMMUNITY_ORIGIN") != null,
                    AbnormallyLowTendersExcluded = awardedContract?.Element("TENDERS_EXCLUDED") != null ? true : false,
                    AwardedToTendererWithVariant = awardedContract?.Element("AWARDED_TENDERER_VARIANT") != null ? true : false,

                },
                DisagreeContractorInformationToBePublished = awardedContract?.Element("CONTRACTORS")?.Attribute("PUBLICATION")?.Value == "NO",

                NumberOfTenders = ParseNumberOfTenders(awardedContract?.Element("TENDERS")),
                ProportionOfValue = ParseInt(awardedContract?.Element("PCT_SUBCONTRACTING")?.Value),
                SubcontractingDescription = ParsePElements(awardedContract?.Element("INFO_ADD_SUBCONTRACTING"), 0),
                PricePaidForBargainPurchases = new ValueContract()
                {
                    Currency = awardedContract?.Element("VAL_BARGAIN_PURCHASE")?.Attribute("CURRENCY")?.Value,
                    Value = ParseDouble(awardedContract?.Element("VAL_BARGAIN_PURCHASE")?.Value)
                }
            };



            return contractAward;
        }

        private static ValueRangeContractType ParseContractValueType(XElement awardedContract)
        {
            return awardedContract?.Element("VALUES")?.Element("VAL_TOTAL") != null ? ValueRangeContractType.Exact :
                   awardedContract?.Element("VALUES")?.Element("VAL_RANGE_TOTAL") != null ? ValueRangeContractType.Range :
                   ValueRangeContractType.Undefined;
        }

        private static NonAward ParseNoAwardedContract(XElement noAwardedContract)
        {
            var procurementDiscontinued = noAwardedContract?.Element("PROCUREMENT_DISCONTINUED");

            return new NonAward()
            {
                FailureReason = procurementDiscontinued != null ? NonAwardFailureReason.AwardDiscontinued :
                                noAwardedContract?.Element("PROCUREMENT_UNSUCCESSFUL") != null ? NonAwardFailureReason.AwardNoTenders :
                                NonAwardFailureReason.Undefined,
                OriginalEsender = new Esender()
                {
                    CustomerLogin = procurementDiscontinued?.Element("CUSTOMER_LOGIN")?.Value,
                    TedNoDocExt = procurementDiscontinued?.Element("NO_DOC_EXT")?.Value,
                    Login = procurementDiscontinued?.Element("ESENDER_LOGIN")?.Value

                },
                OriginalNoticeSentDate = ParseDate(procurementDiscontinued?.Element("DATE_DISPATCH_ORIGINAL")?.Value),
                OriginalNoticeSentVia = procurementDiscontinued?.Element("ORIGINAL_TED_ESENDER") != null ? NonAwardOriginalNoticeSentVia.IcarEsender :
                                        procurementDiscontinued?.Element("ORIGINAL_ENOTICES") != null ? NonAwardOriginalNoticeSentVia.IcarEnotices :
                                        procurementDiscontinued?.Element("ORIGINAL_OTHER_MEANS") != null ? NonAwardOriginalNoticeSentVia.OtherMeans : NonAwardOriginalNoticeSentVia.Undefined,
                OriginalNoticeSentViaOther = procurementDiscontinued?.Element("ORIGINAL_OTHER_MEANS")?.Value
            };
        }

        private static NumberOfTenders ParseNumberOfTenders(XElement tenders)
        {
            return new NumberOfTenders()
            {
                Sme = ParseInt(tenders?.Element("NB_TENDERS_RECEIVED_SME")?.Value),
                OtherEu = ParseInt(tenders?.Element("NB_TENDERS_RECEIVED_SME")?.Value),
                NonEu = ParseInt(tenders?.Element("NB_TENDERS_RECEIVED_NON_EU")?.Value),
                Electronic = ParseInt(tenders?.Element("NB_TENDERS_RECEIVED_EMEANS")?.Value != null ? tenders?.Element("NB_TENDERS_RECEIVED_EMEANS")?.Value : tenders?.Element("OFFERS_RECEIVED_NUMBER_MEANING")?.Value),
                Total = ParseInt(tenders?.Element("NB_TENDERS_RECEIVED")?.Value != null ? tenders?.Element("NB_TENDERS_RECEIVED")?.Value : tenders?.Element("OFFERS_RECEIVED_NUMBER")?.Value)
            };
        }

        private static string[] ParseNutsCodes(XNamespace nutsSchema, XElement objectDescription)
        {
            return objectDescription?.Elements(nutsSchema + "NUTS")?.Select(p => p.Attribute("CODE")?.Value).Distinct().ToArray();
        }

        private static EuFunds ParseEuFunds(XElement objectDescription)
        {
            return new EuFunds()
            {
                ProcurementRelatedToEuProgram = objectDescription.Element("EU_PROGR_RELATED") != null,
                ProjectIdentification = ParsePElements(objectDescription.Element("EU_PROGR_RELATED"))
            };
        }

        private static TimeFrame ParseTimeFrame(XElement objectDescription)
        {
            if (objectDescription == null)
            {
                return null;
            }

            var timeFrame = new TimeFrame()
            {
                BeginDate = ParseDate(objectDescription.Element("DATE_START")?.Value),
                EndDate = ParseDate(objectDescription.Element("DATE_END")?.Value),
                CanBeRenewed = objectDescription.Element("NO_RENEWAL") == null && objectDescription.Element("RENEWAL") != null,
                Months = 0,
                Days = 0,
                RenewalDescription = ParsePElements(objectDescription.Element("RENEWAL_DESCR"), 0),
                Type = objectDescription.Element("DURATION")?.Attribute("TYPE")?.Value == "MONTH" ? TimeFrameType.Months :
                       objectDescription.Element("DURATION")?.Attribute("TYPE")?.Value == "DAY" ? TimeFrameType.Days :
                       objectDescription.Element("DATE_START") != null ? TimeFrameType.BeginAndEndDate :
                       objectDescription.Element("MONTHS") != null ? TimeFrameType.Months :
                       objectDescription.Element("DAYS") != null ? TimeFrameType.Days :
                       TimeFrameType.Undefined,

            };

            if (timeFrame.Type == TimeFrameType.Months)
            {
                timeFrame.Months = ParseInt(objectDescription.Element("DURATION")?.Value != null ? objectDescription.Element("DURATION")?.Value : objectDescription.Element("MONTHS")?.Value);
            }
            if (timeFrame.Type == TimeFrameType.Days)
            {
                timeFrame.Days = ParseInt(objectDescription.Element("DURATION")?.Value != null ? objectDescription.Element("DURATION")?.Value : objectDescription.Element("DAYS")?.Value);
            }


            return timeFrame;
        }

        private static TimeFrame ParseNationalTimeFrame(XElement timeFrameElement)
        {
            if (timeFrameElement == null)
            {
                return null;
            }

            var timeFrame = new TimeFrame()
            {
                BeginDate = ParseDateTimeFromElements(timeFrameElement.Element("START_DATE")),
                EndDate = ParseDateTimeFromElements(timeFrameElement.Element("END_DATE")),
                Type = timeFrameElement.Element("START_DATE") != null ? TimeFrameType.BeginAndEndDate : TimeFrameType.Undefined,
            };
            if (timeFrameElement?.Name?.LocalName == "PROCEDURE_DATE_STARTING")
            {
                timeFrame.ScheduledStartDateOfAwardProcedures = ParseDateTimeFromElements(timeFrameElement);
            }

            return timeFrame;
        }


        private static CpvCode[] ParseAdditionalCpvCodes(XElement objectDescription)
        {
            return objectDescription?.Elements("CPV_ADDITIONAL").Select(element => new CpvCode
            {
                Code = element?.Element("CPV_CODE")?.Attribute("CODE")?.Value,
                VocCodes = element?.Elements("CPV_SUPPLEMENTARY_CODE")?.Select(s => new VocCode { Code = s.Attribute("CODE")?.Value }).ToArray()
            }).ToArray() ?? new CpvCode[0];
        }

        private static OptionsAndVariants ParseOptionsAndVariants(XElement objectDescription)
        {
            var optionsAndVariants = new OptionsAndVariants()
            {
                VariantsWillBeAccepted = objectDescription.Element("NO_ACCEPTED_VARIANTS") != null ? false :
                                         objectDescription.Element("ACCEPTED_VARIANTS") != null ? true :
                                         false,
                Options = objectDescription.Element("OPTIONS") != null ? true :
                          objectDescription.Element("NO_OPTIONS") != null ? false :
                          false
            };

            if (optionsAndVariants.Options == true)
            {
                optionsAndVariants.OptionsDescription = ParsePElements(objectDescription.Element("OPTIONS_DESCR"), 0);
            }

            return optionsAndVariants;
        }

        private static ValueContract ParseValueContract(XElement valueElement)
        {
            if (valueElement == null)
            {
                return new ValueContract();
            }

            return new ValueContract()
            {
                Currency = valueElement.Attribute("CURRENCY")?.Value != null ? valueElement.Attribute("CURRENCY")?.Value : string.Empty,
                Value = ParseDouble(!string.IsNullOrEmpty(valueElement.Value) ? valueElement.Value : valueElement?.Element("VALUE_COST")?.Value)
            };
        }

        private static ValueRangeContract ParseValueRangeContract(XElement valueElement, XElement rangeElement = null)
        {
            if (valueElement != null)
            {
                return new ValueRangeContract()
                {
                    Currency = valueElement.Attribute("CURRENCY")?.Value != null ? valueElement.Attribute("CURRENCY")?.Value :
                               valueElement?.Element("COSTS_RANGE_AND_CURRENCY")?.Attribute("CURRENCY")?.Value,
                    MinValue = ParseDouble(valueElement?.Element("COSTS_RANGE_AND_CURRENCY")?.Element("RANGE_VALUE_COST")?.Element("LOW_VALUE")?.Value),
                    MaxValue = ParseDouble(valueElement?.Element("COSTS_RANGE_AND_CURRENCY")?.Element("RANGE_VALUE_COST")?.Element("HIGH_VALUE")?.Value),
                    Value = ParseDouble(valueElement.Value) != null ? ParseDouble(valueElement.Value) : ParseDouble(valueElement?.Element("VALUE_COST")?.Value != null ? valueElement?.Element("VALUE_COST")?.Value : valueElement?.Element("COSTS_RANGE_AND_CURRENCY")?.Element("VALUE_COST")?.Value),
                    Type = valueElement.Name.LocalName == "VAL_TOTAL" || valueElement?.Element("VALUE_COST") != null || valueElement?.Element("COSTS_RANGE_AND_CURRENCY")?.Element("VALUE_COST") != null ? ValueRangeContractType.Exact : valueElement?.Element("COSTS_RANGE_AND_CURRENCY")?.Element("RANGE_VALUE_COST")?.Name.LocalName == "RANGE_VALUE_COST" ? ValueRangeContractType.Range : ValueRangeContractType.Undefined,
                    DisagreeToBePublished = valueElement?.Attribute("PUBLICATION")?.Value == "NO" ? true : false,
                };
            }
            else if (rangeElement != null)
            {
                return new ValueRangeContract()
                {
                    Currency = rangeElement?.Attribute("CURRENCY")?.Value,
                    MaxValue = ParseDouble(rangeElement?.Element("HIGH")?.Value != null ? rangeElement?.Element("HIGH")?.Value : rangeElement?.Element("RANGE_VALUE_COST")?.Element("HIGH_VALUE")?.Value),
                    MinValue = ParseDouble(rangeElement?.Element("LOW")?.Value != null ? rangeElement?.Element("LOW")?.Value : rangeElement?.Element("RANGE_VALUE_COST")?.Element("LOW_VALUE")?.Value),
                    Type = ValueRangeContractType.Range,
                    DisagreeToBePublished = rangeElement?.Attribute("PUBLICATION")?.Value == "NO" ? true : false,
                };
            }

            return new ValueRangeContract();

        }

        private static AwardCriteria ParseCriterionTypes(XElement acElement)
        {
            var ac = new AwardCriteria()
            {
                CriterionTypes = AwardCriteriaCriterionTypes.Undefined
            };

            if (acElement == null)
            {
                return ac;
            };

            var hasDescriptiveCriteria = acElement?.Element("AC_PROCUREMENT_DOC") != null;
            if (hasDescriptiveCriteria)
            {
                ac.CriterionTypes = AwardCriteriaCriterionTypes.DescriptiveCriteria;
                return ac;
            };



            Func<XElement, AwardCriterionDefinition> parseCriterion = (XElement acc) => new AwardCriterionDefinition()
            {
                Criterion = acc.Element("AC_CRITERION")?.Value,
                Weighting = acc.Element("AC_WEIGHTING")?.Value
            };

            if (acElement.Elements("AC_QUALITY").Any())
            {
                ac.CriterionTypes |= AwardCriteriaCriterionTypes.QualityCriterion;
                ac.QualityCriteria = acElement.Elements("AC_QUALITY").Select(parseCriterion).ToArray();
            }

            if (acElement.Elements("AC_PRICE").Any())
            {
                ac.CriterionTypes |= AwardCriteriaCriterionTypes.PriceCriterion;
                ac.PriceCriterion = parseCriterion(acElement.Element("AC_PRICE"));
            }

            if (acElement.Elements("AC_COST").Any())
            {
                ac.CriterionTypes |= AwardCriteriaCriterionTypes.CostCriterion;
                ac.CostCriteria = acElement.Elements("AC_COST").Select(parseCriterion).ToArray();
            }

            if (acElement.Elements("AC_CRITERION").Any())
            {
                ac.Criterion = acElement.Elements("AC_CRITERION").Select(e => e.Value).ToArray();
                ac.CriterionTypes = AwardCriteriaCriterionTypes.AwardCriteriaDescrBelow;
            }

            return ac;
        }

        private static ComplementaryInformation ParseComplementaryInformation(XElement complementaryInfo)
        {
            return new ComplementaryInformation()
            {
                AdditionalInformation = ParsePElements(complementaryInfo?.Element("INFO_ADD"), 0),
                IsRecurringProcurement = complementaryInfo?.Element("RECURRENT_PROCUREMENT") != null,
                ElectronicInvoicingUsed = complementaryInfo?.Element("EINVOICING") != null,
                ElectronicOrderingUsed = complementaryInfo?.Element("EORDERING") != null,
                ElectronicPaymentUsed = complementaryInfo?.Element("EPAYMENT") != null,
                EstimatedTimingForFurtherNoticePublish = ParsePElements(complementaryInfo?.Element("ESTIMATED_TIMING"), 0),
                
            };
        }

        private static ProcurementProjectContract ParseProject(XElement objectContract, XElement addressContractinBody, XElement contractingBody, NoticeContractType noticeType, string directive)
        {
            var procurementProjectContract = new ProcurementProjectContract
            {
                Title = objectContract.Element("TITLE")?.Element("P")?.Value,
                Organisation = ParseOrganisation(addressContractinBody, contractingBody, noticeType),
                ContractType = ParseContractType(objectContract?.Element("TYPE_CONTRACT")?.Attribute("CTYPE")?.Value),
                
                CentralPurchasing = contractingBody.Element("CENTRAL_PURCHASING") != null ? true : false,
                ReferenceNumber = objectContract.Element("REFERENCE_NUMBER")?.Value,
                ProcurementCategory = GetProcurementCategoryByDirective(directive),
                JointProcurement = contractingBody.Element("JOINT_PROCUREMENT_INVOLVED") != null,
                ProcurementLaw = ParsePElements(contractingBody.Element("PROCUREMENT_LAW")),
                Publish = ProcurementProjectContractPublish.ToTed,
                CoPurchasers = ParseCoPurchasers(contractingBody.Elements("ADDRESS_CONTRACTING_BODY_ADDITIONAL"))
            };

            return procurementProjectContract;

        }

        private static List<ContractBodyContactInformation> ParseCoPurchasers(IEnumerable<XElement> elements)
        {
            return elements.Select(contractingBody => ParseContractingBodyInformation(_nutsSchema, contractingBody)).ToList();

        }

        private static OrganisationContract ParseOrganisation(XElement addressContractinBody, XElement contractingBody, NoticeContractType noticeType)
        {
            var organisationContract = new OrganisationContract()
            {
                Information = ParseContractingBodyInformation(_nutsSchema, addressContractinBody),

            };

            ParseCeActivity(contractingBody, organisationContract);

            ParseCaTypeAndActivity(contractingBody, organisationContract);

            return organisationContract;
        }

        private static void ParseCaTypeAndActivity(XElement contractingBody, OrganisationContract organisationContract)
        {
            if (contractingBody.Element("CA_TYPE") != null)
            {
                organisationContract.ContractingAuthorityType =
                    FromTEDFormatContractingAuthorityType(contractingBody.Element("CA_TYPE")?.Attribute("VALUE")?.Value);
            }
            else if (contractingBody.Element("CA_TYPE_OTHER") != null)
            {
                organisationContract.ContractingAuthorityType = OrganisationContractContractingAuthorityType.OtherType;
                organisationContract.OtherContractingAuthorityType = contractingBody.Element("CA_TYPE_OTHER")?.Value;
            }

            if (contractingBody.Element("CA_ACTIVITY_OTHER") != null)
            {
                organisationContract.MainActivity = OrganisationContractMainActivity.OtherActivity;
                organisationContract.OtherMainActivity = contractingBody.Element("CA_ACTIVITY_OTHER")?.Value;
                organisationContract.ContractingType = OrganisationContractContractingType.ContractingAuthority;

            }
            else if (contractingBody.Element("CA_ACTIVITY") != null)
            {
                organisationContract.MainActivity =
                    FromTEDFormatMainActivity(contractingBody.Element("CA_ACTIVITY")?.Attribute("VALUE")?.Value);
                organisationContract.ContractingType = OrganisationContractContractingType.ContractingAuthority;

            }
        }

        private static void ParseCeActivity(XElement contractingBody, OrganisationContract organisationContract)
        {
            if (contractingBody.Element("CE_ACTIVITY_OTHER") != null)
            {
                organisationContract.MainActivity = OrganisationContractMainActivity.OtherActivity;
                organisationContract.OtherMainActivity = contractingBody.Element("CE_ACTIVITY_OTHER")?.Value;
                organisationContract.ContractingType = OrganisationContractContractingType.ContractingEntity;
            }
            else if (contractingBody.Element("CE_ACTIVITY") != null)
            {
                organisationContract.MainActivityUtilities =
                    FromTEDFormat(contractingBody.Element("CE_ACTIVITY")?.Attribute("VALUE")?.Value);
                organisationContract.ContractingType = OrganisationContractContractingType.ContractingEntity;
            }
        }


        private static ProcurementProjectContractProcurementCategory GetProcurementCategoryByDirective(string value)
        {
            switch (value)
            {
                case DirectiveMapper.EuDefenceProcurements2009Directive:
                    return ProcurementProjectContractProcurementCategory.Defence;
                case DirectiveMapper.EuConcessionProcurement2014Directive:
                    return ProcurementProjectContractProcurementCategory.Lisence;
                case DirectiveMapper.EuPublicProcurements2014Directive:
                    return ProcurementProjectContractProcurementCategory.Public;
                case DirectiveMapper.EuUtilitiesProcurements2014Directive:
                    return ProcurementProjectContractProcurementCategory.Utility;
                default:
                    return ProcurementProjectContractProcurementCategory.Public;
            }
        }

        private static OrganisationContractContractingAuthorityType FromTEDFormatContractingAuthorityType(string type)
        {
            switch (type)
            {
                case "STATE_AUTHORITY":
                case "MINISTRY":
                    return OrganisationContractContractingAuthorityType.MaintypeMinistry;
                case "NATIONAL_AGENCY":
                    return OrganisationContractContractingAuthorityType.MaintypeNatagency;
                case "REGIONAL_AUTHORITY":
                case "MUNICIPALITY":
                    return OrganisationContractContractingAuthorityType.MaintypeLocalauth;
                case "REGIONAL_AGENCY":
                    return OrganisationContractContractingAuthorityType.MaintypeLocalagency;
                case "STATE_ENTERPRISE":
                case "BODY_PUBLIC":
                    return OrganisationContractContractingAuthorityType.MaintypePublicbody;
                case "EU_INSTITUTION":
                    return OrganisationContractContractingAuthorityType.MaintypeEu;
                case "STATE_CHURCH":
                    return OrganisationContractContractingAuthorityType.MaintypeChurch;
                case "OTHER":
                    return OrganisationContractContractingAuthorityType.OtherType;
                default:
                    return OrganisationContractContractingAuthorityType.Undefined;
            }
        }

        private static OrganisationContractMainActivityUtilities FromTEDFormat(string activity)
        {
            switch (activity)
            {
                case "PRODUCTION_TRANSPORT_DISTRIBUTION_GAS_HEAT":
                    return OrganisationContractMainActivityUtilities.MainactivGasProduct;
                case "ELECTRICITY":
                    return OrganisationContractMainActivityUtilities.MainactivElectricity;
                case "EXPLORATION_EXTRACTION_GAS_OIL":
                    return OrganisationContractMainActivityUtilities.MainactivGasExplor;
                case "EXPLORATION_EXTRACTION_COAL_OTHER_SOLID_FUEL":
                    return OrganisationContractMainActivityUtilities.MainactivCoal;
                case "WATER":
                    return OrganisationContractMainActivityUtilities.MainactivWater;
                case "POSTAL_SERVICES":
                    return OrganisationContractMainActivityUtilities.MainactivPostal;
                case "RAILWAY_SERVICES":
                    return OrganisationContractMainActivityUtilities.MainactivRailway;
                case "URBAN_RAILWAY_TRAMWAY_TROLLEYBUS_BUS_SERVICES":
                    return OrganisationContractMainActivityUtilities.MainactivBus;
                case "PORT_RELATED_ACTIVITIES":
                    return OrganisationContractMainActivityUtilities.MainactivPort;
                case "AIRPORT_RELATED_ACTIVITIES":
                    return OrganisationContractMainActivityUtilities.MainactivAirportrelated;
                default:
                    return OrganisationContractMainActivityUtilities.Undefined;
            }
        }
        private static OrganisationContractMainActivity FromTEDFormatMainActivity(string activity)
        {
            switch (activity)
            {
                case "GENERAL_PUBLIC_SERVICES":
                    return OrganisationContractMainActivity.MainactivGeneral;
                case "DEFENCE":
                    return OrganisationContractMainActivity.MainactivDefence;
                case "ECONOMIC_AND_FINANCIAL_AFFAIRS":
                    return OrganisationContractMainActivity.MainactivEconomic;
                case "EDUCATION":
                    return OrganisationContractMainActivity.MainactivEducation;
                case "ENVIRONMENT":
                    return OrganisationContractMainActivity.MainactivEnvironment;
                case "HEALTH":
                    return OrganisationContractMainActivity.MainactivHealth;
                case "HOUSING_AND_COMMUNITY_AMENITIES":
                    return OrganisationContractMainActivity.MainactivHousing;
                case "PUBLIC_ORDER_AND_SAFETY":
                    return OrganisationContractMainActivity.MainactivSafety;
                case "RECREATION_CULTURE_AND_RELIGION":
                    return OrganisationContractMainActivity.MainactivCulture;
                case "SOCIAL_PROTECTION":
                    return OrganisationContractMainActivity.MainactivSocial;
                default:
                    return OrganisationContractMainActivity.Undefined;
            }
        }

        private static ProcurementProjectContractContractType ParseContractType(string value)
        {
            switch (value)
            {
                case "SUPPLIES":
                    return ProcurementProjectContractContractType.Supplies;
                case "SERVICES":
                case "DESIGN_CONTEST":
                    return ProcurementProjectContractContractType.Services;
                case "SOCIALSERVICES":
                    return ProcurementProjectContractContractType.SocialServices;
                case "WORKS":
                    return ProcurementProjectContractContractType.Works;
                case "EDUCATION_SERVICES_WITH_EMPLOYMENT_AUTHORITY":
                    return ProcurementProjectContractContractType.EducationalServices;
                default:
                    return ProcurementProjectContractContractType.Undefined;
            }
        }

        public static ProcurementProjectContractDefenceSupplies ParseSuppliesTEDFormat(string supplies)
        {
            switch (supplies)
            {
                case "COMBINATION_THESE":
                    return ProcurementProjectContractDefenceSupplies.Combination;
                case "HIRE_PURCHASE":
                    return ProcurementProjectContractDefenceSupplies.HirePurchase;
                case "LEASE":
                    return ProcurementProjectContractDefenceSupplies.Lease;
                case "PURCHASE":
                    return ProcurementProjectContractDefenceSupplies.Purchase;
                case "RENTAL":
                    return ProcurementProjectContractDefenceSupplies.Rental;
                default:
                    return ProcurementProjectContractDefenceSupplies.Undefined;
            }
        }

        private static DateTime? ParseDate(string date)
        {
            if (string.IsNullOrEmpty(date))
            {
                return new DateTime?();
            }
            return DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static ContractBodyContactInformation ParseContractingBodyInformation(XNamespace nutsSchema, XElement addressContractinBody)
        {
            if (addressContractinBody == null)
            {
                return null;
            }

            return ParseContractingBodyInformationData(nutsSchema, addressContractinBody);
        }

        private static List<ContractBodyContactInformation> ParseDefenceCoPurchasers(XNamespace nutsSchema, IEnumerable<XElement> contactDataOnBehalfOfContractingAuthority)
        {
            return contactDataOnBehalfOfContractingAuthority?.Select(item => ParseContractingBodyInformationData(nutsSchema, item)).ToList() ?? new List<ContractBodyContactInformation>();
        }

        private static ContractBodyContactInformation ParseContractingBodyInformationData(XNamespace nutsSchema, XElement addressContractinBody)
        {
            return new ContractBodyContactInformation()
            {
                OfficialName = addressContractinBody.Element("OFFICIALNAME")?.Value != null ?
                                           addressContractinBody.Element("OFFICIALNAME")?.Value :
                                           addressContractinBody.Element("ORGANISATION")?.Element("OFFICIALNAME")?.Value,
                NationalRegistrationNumber = addressContractinBody.Element("NATIONALID")?.Value != null ?
                                                         addressContractinBody.Element("NATIONALID")?.Value :
                                                         addressContractinBody.Element("ORGANISATION")?.Element("NATIONALID")?.Value,
                PostalAddress = ParsePostalAddress(addressContractinBody),
                ContactPerson = addressContractinBody.Element("CONTACT_POINT")?.Value ?? addressContractinBody.Element("ATTENTION")?.Value,
                TelephoneNumber = addressContractinBody.Element("PHONE")?.Value,
                Email = addressContractinBody.Element("E_MAIL")?.Value != null ? addressContractinBody.Element("E_MAIL")?.Value :
                                    addressContractinBody.Element("E_MAILS")?.Element("E_MAIL")?.Value != null ? addressContractinBody.Element("E_MAILS")?.Element("E_MAIL")?.Value : "",
                NutsCodes = new[] {
                    addressContractinBody.Element(nutsSchema + "NUTS")?.Attribute("CODE")?.Value
                },
                MainUrl = addressContractinBody.Element("URL_GENERAL")?.Value != null ? addressContractinBody.Element("URL_GENERAL")?.Value :
                                      addressContractinBody?.Parent.Element("INTERNET_ADDRESSES_CONTRACT")?.Element("URL_GENERAL")?.Value != null ? addressContractinBody?.Parent.Element("INTERNET_ADDRESSES_CONTRACT")?.Element("URL_GENERAL")?.Value :
                                      addressContractinBody?.Parent.Element("INTERNET_ADDRESSES_CONTRACT_AWARD")?.Element("URL_GENERAL")?.Value ?? addressContractinBody?.Parent.Element("INTERNET_ADDRESSES_PRIOR_INFORMATION")?.Element("URL_GENERAL")?.Value,
                
            };
        }

        private static PostalAddress ParsePostalAddress(XElement addressData)
        {
            return new PostalAddress
            {
                StreetAddress = addressData?.Element("ADDRESS")?.Value,
                Town = addressData?.Element("TOWN")?.Value,
                PostalCode = addressData?.Element("POSTAL_CODE")?.Value,
                Country = addressData?.Element("COUNTRY")?.Attribute("VALUE")?.Value
            };
        }

        private static string[] ParsePElements(XElement pElements, int lengthLimit = 0)
        {
            if (pElements == null)
            {
                return new string[0];
            }

            var paragraphArray = pElements.Elements("P").Select(p => p.Value).ToArray();
            var totalLength = 0;

            if (lengthLimit > 0)
            {
                return paragraphArray.TakeWhile(p => (totalLength += p.Length) < lengthLimit).ToArray();
            }

            return paragraphArray;
        }



        private XElement ResolveFormElement(NoticeContractType noticeType, XElement formSection, INoticeImportModel form)
        {
            switch (noticeType)
            {
                case NoticeContractType.PriorInformation:
                case NoticeContractType.PriorInformationReduceTimeLimits:
                    return formSection?.Element("F01_2014");
                case NoticeContractType.Contract:
                    return formSection?.Element("F02_2014");
                case NoticeContractType.ContractAward:
                    return formSection?.Element("F03_2014");
                case NoticeContractType.PeriodicIndicativeUtilities:
                    return formSection?.Element("F04_2014");
                case NoticeContractType.ContractUtilities:
                    return formSection?.Element("F05_2014");
                case NoticeContractType.ContractAwardUtilities:
                    return formSection?.Element("F06_2014");
                case NoticeContractType.ExAnte:
                    return formSection?.Element("F15_2014");
                case NoticeContractType.Modification:
                    return formSection?.Element("F20_2014");
                case NoticeContractType.SocialContract:
                case NoticeContractType.SocialPriorInformation:
                case NoticeContractType.SocialContractAward:
                    return formSection?.Element("F21_2014");
                case NoticeContractType.SocialUtilities:
                    return formSection?.Element("F22_2014");
                case NoticeContractType.Concession:
                    return formSection?.Element("F24_2014");
                case NoticeContractType.ConcessionAward:
                    return formSection?.Element("F25_2014");
                case NoticeContractType.DefencePriorInformation:
                    return formSection?.Element("PRIOR_INFORMATION_DEFENCE");
                case NoticeContractType.DefenceContract:
                    return formSection?.Element("CONTRACT_DEFENCE"); //17
                case NoticeContractType.DefenceContractAward:
                    return formSection?.Element("CONTRACT_AWARD_DEFENCE"); //18
                case NoticeContractType.NationalContract:
                case NoticeContractType.NationalPriorInformation:
                case NoticeContractType.NationalDefenceContract:
                    return formSection?.Element("DOMESTIC_CONTRACT") ?? formSection?.Element("GENERAL_CONTRACT"); //form 99 or 91 national contract
                case NoticeContractType.NationalDirectAward:
                    return formSection?.Element("DOMESTIC_DIRECT_AWARD");
                case NoticeContractType.NationalTransparency:
                    return formSection?.Element("DOMESTIC_TRANSPARENCY_NOTICE");
                case NoticeContractType.NationalAgricultureContract:
                    return formSection?.Element("AGRICULTURE_CONTRACT_AUTHORITY") ?? formSection?.Element("AGRICULTURE_CONTRACT_TENDER");
                default:
                    throw new NotSupportedException($"Notice type: {noticeType} , noticeNumber:{form.NoticeNumber}, formNumber:{form.FormNumber}");
            }
        }
    }
}