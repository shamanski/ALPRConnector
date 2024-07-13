using System.ComponentModel.DataAnnotations;

namespace AppDomain
{
    public class Camera
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Name length must be between 3 and 50 characters")]
        public string Name { get; set; }
        
        public string Protocol { get; set; }

        [Required(ErrorMessage = "IP address is required")]
        [AttributeValidators(ErrorMessage = "Invalid IP address format")]
        public string IpAddress { get; set; }

        [Required(ErrorMessage = "Port is required")]
        [NumericRange(1, 65535, ErrorMessage = "Port must be a number between 1 and 65535")]
        public string IpPort { get; set; } = "554";      
        public string Login { get; set; }
        public string Password { get; set; }
        public string Stream { get; set; } = "0";
        public string Manufacturer { get; set; }
        
        public RelativeRectangle Roi { get; set; } = new RelativeRectangle();
    }

    public struct RelativeRectangle 
    {
        public RelativeRectangle()
        {
        }

        public double RelativeRoiLeft { get; set; } = 0;
        public double RelativeRoiTop { get; set; } = 0;
        public double RelativeRoiWidth { get; set; } = 1;
        public double RelativeRoiHeight { get; set; } = 1;
    }

}
