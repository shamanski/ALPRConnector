using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        [IpAddress(ErrorMessage = "Invalid IP address format")]
        public string IpAddress { get; set; }
        
        public string IpPort { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public string RS485Address { get; set; }
    }
}
