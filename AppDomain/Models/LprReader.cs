using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace AppDomain
{
    public class LprReader
    {
        [Required(ErrorMessage = "Name is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Name length must be between 3 and 50 characters")]
        public string Name { get; set; }

        public int RS485Addr { get; set; }

        public ComPortPair ComPortPair { get; set; }

        public Camera Camera { get; set; }
    }
}
