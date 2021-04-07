using System;
using System.Text;
using Hilma.Domain.Exceptions;
using Hilma.Domain.Data.Read;

namespace Hilma.Domain.Integrations.Extensions
{
    public static class EnumExtensions
    {
        public static string ToTEDFormat(this OrganisationContractContractingAuthorityType type)
        {
            switch (type)
            {
                case OrganisationContractContractingAuthorityType.MaintypeMinistry:
                    return "MINISTRY";
                case OrganisationContractContractingAuthorityType.MaintypeNatagency:
                    return "NATIONAL_AGENCY";
                case OrganisationContractContractingAuthorityType.MaintypeLocalauth:
                    return "REGIONAL_AUTHORITY";
                case OrganisationContractContractingAuthorityType.MaintypeLocalagency:
                    return "REGIONAL_AGENCY";
                case OrganisationContractContractingAuthorityType.MaintypePublicbody:
                    return "BODY_PUBLIC";
                case OrganisationContractContractingAuthorityType.MaintypeEu:
                    return "EU_INSTITUTION";
                default:
                    return "";
            }
        }

        public static string ToTEDFormat(this OrganisationContractMainActivity activity)
        {
            switch (activity)
            {
                case OrganisationContractMainActivity.MainactivGeneral:
                    return "GENERAL_PUBLIC_SERVICES";
                case OrganisationContractMainActivity.MainactivDefence:
                    return "DEFENCE";
                case OrganisationContractMainActivity.MainactivEconomic:
                    return "ECONOMIC_AND_FINANCIAL_AFFAIRS";
                case OrganisationContractMainActivity.MainactivEducation:
                    return "EDUCATION";
                case OrganisationContractMainActivity.MainactivEnvironment:
                    return "ENVIRONMENT";
                case OrganisationContractMainActivity.MainactivHealth:
                    return "HEALTH";
                case OrganisationContractMainActivity.MainactivHousing:
                    return "HOUSING_AND_COMMUNITY_AMENITIES";
                case OrganisationContractMainActivity.MainactivSafety:
                    return "PUBLIC_ORDER_AND_SAFETY";
                case OrganisationContractMainActivity.MainactivCulture:
                    return "RECREATION_CULTURE_AND_RELIGION";
                case OrganisationContractMainActivity.MainactivSocial:
                    return "SOCIAL_PROTECTION";
                default:
                    return "";
            }
        }

        public static string ToTEDFormat(this OrganisationContractMainActivityUtilities activity)
        {
            switch (activity)
            {
                case OrganisationContractMainActivityUtilities.MainactivGasProduct:
                    return "PRODUCTION_TRANSPORT_DISTRIBUTION_GAS_HEAT";
                case OrganisationContractMainActivityUtilities.MainactivElectricity:
                    return "ELECTRICITY";
                case OrganisationContractMainActivityUtilities.MainactivGasExplor:
                    return "EXPLORATION_EXTRACTION_GAS_OIL";
                case OrganisationContractMainActivityUtilities.MainactivCoal:
                    return "EXPLORATION_EXTRACTION_COAL_OTHER_SOLID_FUEL";
                case OrganisationContractMainActivityUtilities.MainactivWater:
                    return "WATER";
                case OrganisationContractMainActivityUtilities.MainactivPostal:
                    return "POSTAL_SERVICES";
                case OrganisationContractMainActivityUtilities.MainactivRailway:
                    return "RAILWAY_SERVICES";
                case OrganisationContractMainActivityUtilities.MainactivBus:
                    return "URBAN_RAILWAY_TRAMWAY_TROLLEYBUS_BUS_SERVICES";
                case OrganisationContractMainActivityUtilities.MainactivPort:
                    return "PORT_RELATED_ACTIVITIES";
                case OrganisationContractMainActivityUtilities.MainactivAirportrelated:
                    return "AIRPORT_RELATED_ACTIVITIES";
                default:
                    return "";
            }
        }

        public static string ToTEDFormat(this ProcurementProjectContractContractType type, ProcurementProjectContractProcurementCategory category = ProcurementProjectContractProcurementCategory.Undefined)
        {
            switch (type)
            {
                case ProcurementProjectContractContractType.Supplies:
                    if (category == ProcurementProjectContractProcurementCategory.Lisence)
                    {
                        throw new HilmaException("License legal basis does not support supplies contract type!");
                    }
                    return "SUPPLIES";
                case ProcurementProjectContractContractType.SocialServices:
                case ProcurementProjectContractContractType.Services:
                    return "SERVICES";
                case ProcurementProjectContractContractType.Works:
                    return "WORKS";
                default:
                    return "";
            }
        }
        public static string ToTEDFormat(this ProcurementProjectContractDefenceSupplies type)
        {
            switch (type)
            {
                case ProcurementProjectContractDefenceSupplies.Combination:
                    return "COMBINATION_THESE";
                case ProcurementProjectContractDefenceSupplies.HirePurchase:
                    return "HIRE_PURCHASE";
                case ProcurementProjectContractDefenceSupplies.Lease:
                    return "LEASE";
                case ProcurementProjectContractDefenceSupplies.Purchase:
                    return "PURCHASE";
                case ProcurementProjectContractDefenceSupplies.Rental:
                    return "RENTAL";
                default:
                    return "";
            }
        }

        public static string ToTEDFormat(this ProcedureInformationProcedureType type)
        {
            switch (type)
            {
                case ProcedureInformationProcedureType.ProctypeOpen:
                    return "PT_OPEN";
                case ProcedureInformationProcedureType.ProctypeRestricted:
                    return "PT_RESTRICTED";
                case ProcedureInformationProcedureType.ProctypeCompNegotiation:
                    return "PT_COMPETITIVE_NEGOTIATION";
                case ProcedureInformationProcedureType.ProctypeCompDialogue:
                    return "PT_COMPETITIVE_DIALOGUE";
                case ProcedureInformationProcedureType.ProctypeInnovation:
                    return "PT_INNOVATION_PARTNERSHIP";
                case ProcedureInformationProcedureType.ProctypeNegotiationsInvolved:
                    return "PT_INVOLVING_NEGOTIATION";
                case ProcedureInformationProcedureType.ProctypeNegotiation:
                    return "PT_NEGOTIATED_CHOICE";
                case ProcedureInformationProcedureType.ProctypeNegotiatedWoPub:
                case ProcedureInformationProcedureType.ProctypeConcessionWoPub:
                case ProcedureInformationProcedureType.ProctypeAwardWoCall:
                case ProcedureInformationProcedureType.ProctypeNegotiatedWoNotice:
                    return "PT_NEGOTIATED_WITHOUT_PUBLICATION";
                case ProcedureInformationProcedureType.AwardWoPriorPubD1:
                case ProcedureInformationProcedureType.AwardWoPriorPubD4:
                case ProcedureInformationProcedureType.AwardWoPriorPubD1Other:
                    return "PT_AWARD_CONTRACT_WITHOUT_CALL";
                default:
                    return "";
            }
        }


        public static string ToTEDChangeFormat(this CommunicationInformationSendTendersOption type)
        {
            switch (type)
            {
                case CommunicationInformationSendTendersOption.AddressSendTenders:
                    return "address_send_tenders";
                case CommunicationInformationSendTendersOption.AddressOrganisation:
                    return "address_to_above";
                case CommunicationInformationSendTendersOption.AddressFollowing:
                    return "address_following";
                case CommunicationInformationSendTendersOption.EmailSendTenders:
                    return "email_send_tenders";
                default:
                    return "";
            }
        }

        public static string ToTEDChangeFormat(this AwardContractAwarded type)
        {
            switch (type)
            {
                case AwardContractAwarded.AwardedContract:
                    return "yes";
                case AwardContractAwarded.NoAwardedContract:
                    return "no";
                default:
                    return "";
            }
        }

        /// <summary>
        ///     Generic version of all the crap above, only use if the enum names reflect the exact TED names
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string ToTedChangeFormatGeneric<T>(this T value) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
            {
                throw new ArgumentException("T must be an enumerated type");
            }

            var sb = new StringBuilder();
            var charArray = value.ToString().ToCharArray();

            for (var i = 0; i < charArray.Length; i++)
            {
                var c = charArray[i];
                if (char.IsUpper(c) && i > 0)
                {
                    sb.Append("_");
                }

                sb.Append(c);
            }

            return sb.ToString().ToLowerInvariant();
        }
    }
}
