using Hilma.Domain.Attributes;
using Hilma.Domain.Enums;

namespace Hilma.Domain.Entities
{
    /// <summary>
    ///     Contact information section for additional contracting body information on Hilma form.
    /// </summary>
    //[Owned]
    [Contract]
    public class ContractorContactInformation
    {
        /// <summary>
        /// Contract id, generated by Hilma. Not sent to TED.
        /// </summary>
        public string ContractId { get; set; }

        /// <summary>
        /// Official name of the contracting body 
        /// </summary>
        /// <example>Innofactor Oyj</example>
        //[Required]
        [CorrigendumLabel("name_official", "V.2.3")]
        //[MaxLength(300)]
        public string OfficialName { get; set; }

        /// <summary>
        /// National registration number of the contracting body 
        /// </summary>
        /// <example>1732626-9</example>
        //[Required]
        [CorrigendumLabel("national_id", "V.2.3")]
        //[MaxLength(100)]
        public string NationalRegistrationNumber { get; set; }

        /// <summary>
        /// Location code for the organisation
        /// </summary>
        //[Required]
        //[MinLength(1), MaxLength(20)]
        [CorrigendumLabel("nutscode", "V.2.3")]
        public string[] NutsCodes { get; set; } = new string[0];

        /// <summary>
        ///     Postal address for the contact.
        /// </summary>
        public PostalAddress PostalAddress { get; set; } = new PostalAddress();

        /// <summary>
        /// Phone number for the contact. Format is important.
        /// </summary>
        /// <example>
        /// +358 123123123
        /// </example>
        //[MaxLength(100)]
        [CorrigendumLabel("address_phone", "V.2.3")]
        public string TelephoneNumber { get; set; }

        /// <summary>
        /// Contact email.
        /// </summary>
        /// <example>
        /// tendering@innofactor.com
        /// </example>
        //[MaxLength(200)]
        [CorrigendumLabel("address_email", "V.2.3")]
        public string Email { get; set; }

        /// <summary>
        /// Url, including the protocol, for additional info.
        /// </summary>
        /// <example>
        /// https://www.innofactor.com
        /// </example>
        //[MaxLength(200)]
        [CorrigendumLabel("H_url", "V.2.3")]
        public string MainUrl { get; set; }

        /// <summary>
        /// The contractor is an SME.
        /// </summary>
        [CorrigendumLabel("awarded_sme", "V.2.3")]
        public bool IsSmallMediumEnterprise { get; set; }

        /// <summary>
        /// Vuejs application persistent validation state.
        /// </summary>
        public ValidationState ValidationState { get; set; }
    }
}
