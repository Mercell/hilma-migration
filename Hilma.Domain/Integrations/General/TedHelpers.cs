using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Hilma.Domain.DataContracts;
using Hilma.Domain.Entities;
using Hilma.Domain.Enums;

namespace Hilma.Domain.Integrations.General
{

    /// <summary>
    /// Helpers methods to build XML elements for TED
    /// </summary>
    public class TedHelpers
    {
        public static readonly XNamespace Xmlns = "http://publications.europa.eu/resource/schema/ted/R2.0.9/reception";
        public static readonly XNamespace n2016 = "http://publications.europa.eu/resource/schema/ted/2016/nuts";
        public static readonly XNamespace xs = "http://www.w3.org/2001/XMLSchema-instance";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="notice"></param>
        /// <param name="eSenderLogin"></param>
        /// <param name="tedSenderOrganisation"></param>
        /// <param name="tedContactEmail"></param>
        /// <returns></returns>
        public static XElement LoginPart(NoticeContract notice, string eSenderLogin, string tedSenderOrganisation, string tedContactEmail)
        {
            return Element("SENDER",
                Element("IDENTIFICATION",
                    Element("ESENDER_LOGIN", eSenderLogin),
                      Element("NO_DOC_EXT", notice.NoticeNumber)),
                Element("CONTACT", Element("ORGANISATION", tedSenderOrganisation),
                    Element("COUNTRY", new XAttribute("VALUE", "FI")),
                    Element("E_MAIL", tedContactEmail)));
        }

        /// <summary>
        /// Legal basis. Used in forms F01-F03
        /// </summary>
        /// <param name="notice">The notice</param>
        /// <param name="parent">Needed if noticetype = modification</param>
        /// <returns></returns>
        public static List<XElement> LegalBasis(NoticeContract notice, NoticeContract parent = null)
        {

            string directive = DirectiveMapper.GetDirective(notice, parent);
            return new List<XElement>
            {
                ElementWithAttribute("LEGAL_BASIS", "VALUE", directive),
                directive == "32018R1046" ?
                PElement("LEGAL_BASIS_OTHER", GetOtherContractingAuthorityType(notice)): null
            };
        }

        private static string GetOtherContractingAuthorityType(NoticeContract notice)
        {
            switch (notice.Type)
            {
                case NoticeType.PriorInformation:
                case NoticeType.Contract:
                case NoticeType.ContractAward:
                    return notice.Project.Organisation.OtherContractingAuthorityType;
            }

            return string.Empty;
        }

        

        internal static XDocument CreateTedDocument(params XElement[] xElements)
        {
            return new XDocument(
                 new XDeclaration("1.0", "utf-8", null), TedHelpers.Element("TED_ESENDERS",
                     new XAttribute(XNamespace.Xmlns + nameof(TedHelpers.n2016), TedHelpers.n2016),
                     new XAttribute("VERSION", "R2.0.9.S03"),
                     new XAttribute(XNamespace.Xmlns + nameof(TedHelpers.xs), TedHelpers.xs),
                     xElements));
        } 
        /// <summary>
        /// Contracting authority fields
        /// </summary>
        /// <param name="elementName">Name of element(ADDRESS_CONTRACTING_BODY or ADDRESS_CONTRACTING_BODY_ADDITIONAL)</param>
        /// <param name="organisation">Organisation</param>
        /// <param name="contactPerson">Contact person</param>
        /// <returns>Xelement</returns>
            public static XElement ADDRS1(string elementName, OrganisationContract organisation, ContactPerson contactPerson)
        {
            return Element(elementName,
                    Element("OFFICIALNAME", organisation.Information.OfficialName),
                    Element("NATIONALID", organisation.Information.NationalRegistrationNumber),
                    Element("ADDRESS", organisation.Information.PostalAddress.StreetAddress),
                    Element("TOWN", organisation.Information.PostalAddress.Town),
                    Element("POSTAL_CODE", organisation.Information.PostalAddress.PostalCode),
                    ElementWithAttribute("COUNTRY", "VALUE", organisation.Information.PostalAddress.Country),
                    !string.IsNullOrEmpty(contactPerson.Name) ? Element("CONTACT_POINT", contactPerson.Name) : null,
                    Element("PHONE", contactPerson.Phone),
                    Element("E_MAIL", contactPerson.Email),
                    organisation.Information.NutsCodes.ToList().Select(x => new XElement(n2016 + "NUTS", new XAttribute("CODE", x))),
                    Element("URL_GENERAL", organisation.Information.MainUrl),
                    Element("URL_BUYER", organisation.Information.BuyerProfileUrl)
                );
        }

        /// <summary>
        /// ADDR-S1
        /// </summary>
        /// <param name="elementName">Name of element(ADDRESS_CONTRACTING_BODY or ADDRESS_CONTRACTING_BODY_ADDITIONAL)</param>
        /// <param name="information">Contract body contact</param>
        /// <returns>XElement</returns>
        public static XElement ADDRS1(string elementName, ContractBodyContactInformation information)
        {
            if (information == null)
            {
                return null;
            }
            return Element(elementName,
                Element("OFFICIALNAME", information.OfficialName),
                Element("NATIONALID", information.NationalRegistrationNumber),
                Element("ADDRESS", information.PostalAddress.StreetAddress),
                Element("TOWN", information.PostalAddress.Town),
                Element("POSTAL_CODE", information.PostalAddress.PostalCode),
                ElementWithAttribute("COUNTRY", "VALUE", information.PostalAddress.Country),
                Element("E_MAIL", information.Email),
                information.NutsCodes.ToList().Select(x => new XElement(n2016 + "NUTS", new XAttribute("CODE", x))),
                Element("URL_GENERAL", information.MainUrl),
                Element("URL_BUYER", information.BuyerProfileUrl)
            );
        }

        /// <summary>
        /// Address (section 5)
        /// </summary>
        /// <param name="contractor">Contractor contat information</param>
        /// <returns>Xelement</returns>
        public static XElement ADDRS5(ContractorContactInformation contractor)
        {
            if (contractor == null)
                return null;

            return Element("ADDRESS_CONTRACTOR",
                    Element("OFFICIALNAME", contractor.OfficialName),
                    Element("NATIONALID", contractor.NationalRegistrationNumber),
                    Element("ADDRESS", contractor.PostalAddress.StreetAddress),
                    Element("TOWN", contractor.PostalAddress.Town),
                    Element("POSTAL_CODE", contractor.PostalAddress.PostalCode),
                    ElementWithAttribute("COUNTRY", "VALUE", contractor.PostalAddress.Country),
                    Element("PHONE", contractor.TelephoneNumber),
                    Element("E_MAIL", contractor.Email),
                    contractor.NutsCodes.ToList().Select(x => new XElement(n2016 + "NUTS", new XAttribute("CODE", x))),
                    Element("URL", contractor.MainUrl)
                );
        }
        /// <summary>
        /// ADDR-S6 Review body
        /// </summary>
        /// <param name="elementName"></param>
        /// <param name="information"></param>
        /// <returns></returns>
        public static XElement ADDRS6(string elementName, ContractBodyContactInformation information)
        {
            if (information == null)
                return null;

            return Element(elementName,
                Element("OFFICIALNAME", information.OfficialName),
                Element("ADDRESS", information.PostalAddress.StreetAddress),
                Element("TOWN", information.PostalAddress.Town),
                Element("POSTAL_CODE", information.PostalAddress.PostalCode),
                ElementWithAttribute("COUNTRY", "VALUE", information.PostalAddress.Country),
                Element("PHONE", information.TelephoneNumber),
                Element("E_MAIL", information.Email),
                Element("URL", information.MainUrl)
            );
        }
              

        /// <summary>
        /// 2019-000000-000 	vuosi-id-lot
        /// </summary>
        /// <param name="notice">Notice conrtact</param>
        /// <param name="lotNumber">lot number</param>
        /// <returns></returns>
        public static string GetContractNumber(NoticeContract notice, int lotNumber)
        {
            return $"{DateTime.Now.Year}-{(notice.Id % 1000000):D6}-{(lotNumber % 1000):D3}";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elementName"></param>
        /// <param name="attributeName"></param>
        /// <param name="attributeValue"></param>
        /// <param name="elementValue"></param>
        /// <returns></returns>
        public static XElement ElementWithAttribute(string elementName, string attributeName, string attributeValue, string elementValue = null)
        {
            return !string.IsNullOrWhiteSpace(attributeValue)
                ? new XElement(Xmlns + elementName.ToUpper(), new XAttribute(attributeName.ToUpper(), attributeValue), elementValue) : null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elementName"></param>
        /// <param name="attributeName"></param>
        /// <param name="attributeValue"></param>
        /// <param name="elementValue"></param>
        /// <returns></returns>
        public static XElement DateElementWithAttribute(string elementName, string attributeName, string attributeValue, DateTime? elementValue = null)
        {
            return !string.IsNullOrWhiteSpace(attributeValue)
                ? new XElement(Xmlns + elementName.ToUpper(), new XAttribute(attributeName.ToUpper(), attributeValue), elementValue?.ToString("yyyy-MM-dd")) : null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elementName"></param>
        /// <param name="attributeName"></param>
        /// <param name="attributeValue"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static XElement ElementWithAttribute(string elementName, string attributeName, string attributeValue, params object[] value)
        {
            return !string.IsNullOrWhiteSpace(attributeValue)
                ? new XElement(Xmlns + elementName.ToUpper(), new XAttribute(attributeName.ToUpper(), attributeValue), value.Where(a => a != null)) : null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elementName"></param>
        /// <param name="codes"></param>
        /// <returns></returns>
        public static List<XElement> CpvCodeElement(string elementName, CpvCode[] codes){
            if( !codes.Any())
                return null;

            var elements = new List<XElement>();
            foreach(var code in codes.Where( c=> !string.IsNullOrEmpty(c.Code)))
            {
                elements.Add(Element(elementName, ElementWithAttribute("CPV_CODE", "CODE", code.Code),
                    code.VocCodes?.Select(v => ElementWithAttribute("CPV_SUPPLEMENTARY_CODE", "CODE", v.Code))));
            }

            return elements;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="elementName"></param>
        /// <param name="attributeName"></param>
        /// <param name="attributeValue"></param>
        /// <returns></returns>
        public static XElement ElementWithAttribute(string elementName, string attributeName, object attributeValue)
        {
            return attributeValue != null
                ? new XElement(Xmlns + elementName.ToUpper(), new XAttribute(attributeName.ToUpper(), attributeValue)) : null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static XElement Element(string name, string value)
        {
            return !string.IsNullOrEmpty(value?.Trim()) ? new XElement(Xmlns + name.ToUpper(), value) : null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static XElement PElement(string name, string[] value)
        {
            if (value == null || !value.Any())
            {
                return null;
            }

            return new XElement(Xmlns + name.ToUpper(), value.Select(p => new XElement(Xmlns + "P", p)));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static XElement PElement(string name, string value)
        {
            if (value == null || string.IsNullOrEmpty(value))
                return null;

            return new XElement(Xmlns + name.ToUpper(), new XElement(Xmlns + "P", value));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="attribute"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static XElement Element(string name, XAttribute attribute, object value)
        {
            return !(value is string) && value != null || value is string str && !string.IsNullOrEmpty(str?.Trim()) ? new XElement(Xmlns + name.ToUpper(), attribute, value) : null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="minValue"></param>
        /// <returns></returns>
        public static XElement Element(string name, int value, int? minValue = null)
        {
            return !minValue.HasValue || value >= minValue.Value ? new XElement(Xmlns + name.ToUpper(), value) : null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static XElement Element(string name, int? value)
        {
            return value.HasValue ? new XElement(Xmlns + name.ToUpper(), value) : null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static XElement Element(string name, params object[] value)
        {
            return value != null && value.Any(a => a != null) ? new XElement(Xmlns + name.ToUpper(), value.Where(a => a != null)) : null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static XElement Element(string name)
        {
            return new XElement(Xmlns + name.ToUpper());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static XElement Element(XName name, params object[] value)
        {
            return value != null && value.Any(a => a != null) ? new XElement(name, value.Where(a => a != null)) : null;
        }

        public static XElement DateElement(string name, DateTime? value)
        {
            return value != null ? new XElement(Xmlns + name.ToUpper(), value?.ToString("yyyy-MM-dd")) : null;
        }

        public static XElement TimeElement(string name, DateTime? value)
        {
            var timeElement = value.HasValue
                ? new XElement(Xmlns + name.ToUpper(), value?.ToString("hh':'mm"))
                : null;
            return timeElement;
        }
    }
}