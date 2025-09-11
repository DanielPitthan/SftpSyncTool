using System.Net;
using System.Net.Mail;
using Models.Configurations;

namespace Infrastructure.Email
{
    public class Email
    {
        public Email(string to,string subject,string body)
        {
            this.Body = body;
            this.To = to;
            this.Subject = subject;
        }

        public string Body { get; set; }
        public string To { get; set; }
        public string Subject { get; set; }

      
        /// <summary>
        /// Faz o envio do e-mail. Não utiliza envio de anexos, apenas email simples 
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void Send()
        {
            using (MailMessage message = new MailMessage())
            {
                using (SmtpClient smtp = new SmtpClient())
                {
                    message.From = new MailAddress(EmailCredentials.EMAIL_FROM);
                    message.Subject = Subject;
                    message.IsBodyHtml = true;
                    message.Body = Body;


                    //Adiciona os e-mails separados por vírgula
                    var emailsTo = To.Split(',');
                    for (int i = 0; i < emailsTo.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(emailsTo[i]))
                            message.To.Add(new MailAddress(emailsTo[i]));
                    }
                                        
                    smtp.Port = EmailCredentials.EMAIL_PORT;
                    smtp.Host = EmailCredentials.EMAIL_HOST;
                    smtp.EnableSsl = true;

                    smtp.UseDefaultCredentials = false;
                    smtp.Credentials = new NetworkCredential(EmailCredentials.EMAIL_FROM, EmailCredentials.EMAIL_SENHA);
                    smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                    try
                    {
                        smtp.Send(message);
                    }

                    catch (SmtpException ex)
                    {
                        throw new Exception(ex.Message, ex.InnerException);
                    }
                }
            }

        }
    }
}
