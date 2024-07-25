using System.ComponentModel.DataAnnotations;

namespace AppDomain
{
    public class ComPortPair : Model
    {
        public string Name { get; set; }

        [Required(ErrorMessage = "Sender is required")]
        public string Sender { get; set; }

        [Required(ErrorMessage = "Sender is required")]
        public string Receiver { get; set; }
    }
}
