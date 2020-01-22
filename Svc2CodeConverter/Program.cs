using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using FluentNHibernate.Utils;

namespace Svc2CodeConverter
{
    class Program
    {
        private static readonly string ContractsDestinationPath = ConfigurationManager.AppSettings["contracts_destination_path"];
        private static readonly string XsdPath = ConfigurationManager.AppSettings["xsd_path"];

        public static readonly string[] ServicesNames = {

            "https://api.dom.gosuslugi.ru/ext-bus-nsi-common-service/services/NsiCommonAsync"
            ,"https://api.dom.gosuslugi.ru/ext-bus-home-management-service/services/HomeManagementAsync"
            ,"https://api.dom.gosuslugi.ru/ext-bus-bills-service/services/BillsAsync"
            ,"https://api.dom.gosuslugi.ru/ext-bus-device-metering-service/services/DeviceMeteringAsync"
            ,"https://api.dom.gosuslugi.ru/ext-bus-nsi-service/services/NsiAsync"

            //,"https://api.dom.gosuslugi.ru/ext-bus-org-registry-common-service/services/OrgRegistryCommon"
            //,"https://api.dom.gosuslugi.ru/ext-bus-org-registry-common-service/services/OrgRegistryCommonAsync"
            //,"https://api.dom.gosuslugi.ru/ext-bus-org-registry-service/services/OrgRegistry"
            //,"https://api.dom.gosuslugi.ru/ext-bus-org-registry-service/services/OrgRegistryAsync"
            //,"https://api.dom.gosuslugi.ru/ext-bus-organization-service/services/Organization"
            //,"https://api.dom.gosuslugi.ru/ext-bus-organization-service/services/OrganizationAsync"
            //,"https://api.dom.gosuslugi.ru/ext-bus-capital-repair-programs-service/services/CapitalRepair"
            //,"https://api.dom.gosuslugi.ru/ext-bus-capital-repair-programs-service/services/CapitalRepairAsync"
            //,"https://api.dom.gosuslugi.ru/ext-bus-rki-service/services/Infrastructure"
            //,"https://api.dom.gosuslugi.ru/ext-bus-rki-service/services/InfrastructureAsync"
            //,"https://api.dom.gosuslugi.ru/ext-bus-inspection-service/services/Inspection"
            //,"https://api.dom.gosuslugi.ru/ext-bus-inspection-service/services/InspectionAsync"
            //,"https://api.dom.gosuslugi.ru/ext-bus-licenses-service/services/Licenses"
            //,"https://api.dom.gosuslugi.ru/ext-bus-licenses-service/services/LicensesAsync"
            //,"https://api.dom.gosuslugi.ru/ext-bus-msp-service/services/MSP/"
            //,"https://api.dom.gosuslugi.ru/ext-bus-msp-service/services/MSPAsync/"
            //,"https://api.dom.gosuslugi.ru/ext-bus-fas-service/services/FASAsync"
            //,"https://api.dom.gosuslugi.ru/ext-bus-payment-service/services/PaymentAsync"
        };

        static void Main(string[] args)
        {
            /*if (Directory.Exists(ContractsDestinationPath))
                Directory.Delete(ContractsDestinationPath, true);*/

            //ProcessXsd();
            ProcessWsdl();
            
            Console.WriteLine(@"---------------------------------Done!");
            Console.ReadKey();
        }

        /// <summary>
        /// Тянет описания классов для сервисов из файлов в папке
        /// </summary>
        private static void ProcessXsd()
        {
            var xsdCodeDestination = $@"{ContractsDestinationPath}\Xsd2Code";
            Console.WriteLine(@"Process XSD from path");
            Console.WriteLine($"Xsd source path {XsdPath}");
            Console.WriteLine($"Path destination {xsdCodeDestination}");

            if (Directory.Exists(xsdCodeDestination))
            {
                Directory.Delete(xsdCodeDestination, true);
            }

            if (!Directory.Exists(xsdCodeDestination))
            {
                Directory.CreateDirectory(xsdCodeDestination);
                Directory.SetAccessControl(xsdCodeDestination, new DirectorySecurity());
            }

            var ccUnitFromXsd = XsdCodeGen0.GenerateCodeFromXsdFromPath(XsdPath);
            Library.CreateServiceSupportWithUnits(new[] { ccUnitFromXsd }, xsdCodeDestination);
        }

        /// <summary>
        /// Тянет описания сервисов из интернета
        /// </summary>
        private static void ProcessWsdl()
        {
            Console.WriteLine(@"Process wsdls from web network");

            var dtosDestinationPath = $@"{ContractsDestinationPath}\Dtos";
            if (Directory.Exists(dtosDestinationPath))
                Directory.Delete(dtosDestinationPath, true);

            var unitsData = Library.LoadSvcData(ServicesNames/*.Where(s => s.Contains("ext-bus-home-management-service")).ToArray()*/, "Integration.");

            var formattedUnits = Library.GenerateCodeUnits(unitsData);

            if (!Directory.Exists(ContractsDestinationPath))
            {
                Directory.CreateDirectory(ContractsDestinationPath);
                Directory.SetAccessControl(ContractsDestinationPath, new DirectorySecurity());
            }

            Directory.CreateDirectory(dtosDestinationPath);
            Directory.SetAccessControl(dtosDestinationPath, new DirectorySecurity());

            Library.CreateServiceSupportWithUnits(formattedUnits, ContractsDestinationPath);

            var mappedUnits = Library.MapUnits(formattedUnits.DeepClone());

            Library.CreateServiceSupportWithUnits(mappedUnits, dtosDestinationPath, true);
        }
    }
}
