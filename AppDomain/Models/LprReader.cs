using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AppDomain
{
    public class LprReader
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Name length must be between 3 and 50 characters")]
        public string Name { get; set; }


        [Required(ErrorMessage = "RS485 address is required")]
        [NumericRange(1, 128, ErrorMessage = "RS must be a number between 1 and 128")]
        public int RS485Addr { get; set; }

        public string ComPortPairName { get; set; }
        public string CameraName { get; set; }

        [JsonIgnore]
        public ComPortPair ComPortPair { get; set; }

        [JsonIgnore]
        public Camera Camera { get; set; }
    }
}
