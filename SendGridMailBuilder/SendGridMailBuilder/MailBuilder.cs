﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using SendGrid;
using System.Text.RegularExpressions;

namespace SendGrid {
    public sealed class MailBuilder {
        private static readonly Regex TemplateTest = new Regex(@"<%\s*body\s*%>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TextUnsubscribeTest = new Regex(@"<%\s*%>", RegexOptions.Compiled);
        private static readonly Regex HtmlUnsubscribeTest = new Regex(@"<%\s*([^\s%]+\s?)+\s*%>", RegexOptions.Compiled);

        private SendGridMessage sendgrid;
        private bool hideRecipients = false;

        public static MailBuilder Create() {
            var mailBuilder = new MailBuilder();
            mailBuilder.sendgrid = new SendGridMessage();
            return mailBuilder;
        }
        public SendGridMessage Build() {
            var exceptions = new List<Exception>();
            if (string.IsNullOrEmpty(sendgrid.Html) && string.IsNullOrEmpty(sendgrid.Text)) {
                exceptions.Add(new InvalidOperationException("Mail does not contain a body."));
            }
            if (sendgrid.To.Length == 0) { exceptions.Add(new InvalidOperationException("Mail does not have any recipients.")); }
            if (sendgrid.From == null) { exceptions.Add(new InvalidOperationException("Mail does not have a valid sender's email address.")); }
            if (string.IsNullOrEmpty(sendgrid.Subject)) { exceptions.Add(new InvalidOperationException("Mail does not have a subject.")); }
            if (exceptions.Count > 0) { throw new AggregateException("Mail has one or more issues and cannot be built.  Check InnerExceptions for details.", exceptions); }
            if (hideRecipients) {
                sendgrid.Header.SetTo(sendgrid.To.ToListString());
                sendgrid.To = new MailAddress[1] { sendgrid.From };
            }
            return sendgrid;
        }

        public MailBuilder HideRecipients() {
            hideRecipients = true;
            return this;
        }

        public MailBuilder From(MailAddress address) {
            sendgrid.From = address;
            return this;
        }
        public MailBuilder From(string email) {
            sendgrid.From = new MailAddress(email);
            return this;
        }
        public MailBuilder From(string email, string displayName) {
            sendgrid.From = new MailAddress(email, displayName);
            return this;
        }

        public MailBuilder To(MailAddress address) {
            return this.To(address.Address, address.DisplayName);
        }
        public MailBuilder To(string email) {
            sendgrid.AddTo(email);
            return this;
        }
        public MailBuilder To(string email, string displayName) {
            sendgrid.AddTo(EmailFormat(displayName, email));
            return this;
        }
        public MailBuilder To(IEnumerable<string> addresses) {
            sendgrid.AddTo(addresses);
            return this;
        }
        public MailBuilder To(IEnumerable<MailAddress> addresses) {
            return this.To(addresses.ToListString());
        }
        public MailBuilder To(MailAddressCollection addresses) {
            return this.To(addresses.ToList());
        }

        public MailBuilder Cc(MailAddress address) {
            var currentCc = new List<MailAddress>(sendgrid.Cc);
            currentCc.Add(address);
            sendgrid.Cc = currentCc.ToArray();
            return this;
        }
        public MailBuilder Cc(string email) {
            return this.Cc(new MailAddress(email));
        }
        public MailBuilder Cc(string email, string displayName) {
            return this.Cc(new MailAddress(email, displayName));
        }
        public MailBuilder Cc(IEnumerable<string> addresses) {
            List<MailAddress> newAddresses = addresses.ToList().ConvertAll<MailAddress>((x) => { return new MailAddress(x); });
            return this.Cc(newAddresses);
        }
        public MailBuilder Cc(IEnumerable<MailAddress> addresses) {
            var currentCc = new List<MailAddress>(sendgrid.Cc);
            currentCc.AddRange(addresses);
            sendgrid.Cc = currentCc.ToArray();
            return this;
        }
        public MailBuilder Cc(MailAddressCollection addresses) {
            return this.Cc(addresses.ToList());
        }

        public MailBuilder Bcc(MailAddress address) {
            var currentBcc = new List<MailAddress>(sendgrid.Bcc);
            currentBcc.Add(address);
            sendgrid.Bcc = currentBcc.ToArray();
            return this;
        }
        public MailBuilder Bcc(string email) {
            return this.Bcc(new MailAddress(email));
        }
        public MailBuilder Bcc(string email, string displayName) {
            return this.Bcc(new MailAddress(email, displayName));
        }
        public MailBuilder Bcc(IEnumerable<string> addresses) {
            List<MailAddress> newAddresses = addresses.ToList().ConvertAll<MailAddress>((x) => { return new MailAddress(x); });
            return this.Bcc(newAddresses);
        }
        public MailBuilder Bcc(IEnumerable<MailAddress> addresses) {
            var currentBcc = new List<MailAddress>(sendgrid.Bcc);
            currentBcc.AddRange(addresses);
            sendgrid.Bcc = currentBcc.ToArray();
            return this;
        }
        public MailBuilder Bcc(MailAddressCollection addresses) {
            return this.Bcc(addresses.ToList());
        }

        public MailBuilder Subject(string subject) {
            sendgrid.Subject = subject;
            return this;
        }

        public MailBuilder Html(AlternateView view) {
            return this
                .Html(GetAlternateViewAsString(view))
                .EmbedImages(view.LinkedResources);
        }
        public MailBuilder Html(string html) {
            sendgrid.Html = html;
            return this;
        }

        public MailBuilder Text(AlternateView view) {
            return this
                .Text(GetAlternateViewAsString(view));
        }
        public MailBuilder Text(string text) {
            sendgrid.Text = text;
            return this;
        }

        public MailBuilder AttachFile(Stream stream, string name) {
            sendgrid.AddAttachment(stream, name);
            return this;
        }
        public MailBuilder AttachFile(string filePath) {
            sendgrid.AddAttachment(filePath);
            return this;
        }
        public MailBuilder AttachFile(Attachment attachment) {
            return this.AttachFile(attachment.ContentStream, attachment.Name);
        }
        public MailBuilder AttachFiles(IEnumerable<Attachment> attachments) {
            foreach (var attachment in attachments) { this.AttachFile(attachment); }
            return this;
        }
        public MailBuilder AttachFiles(AttachmentCollection attachments) {
            return this.AttachFiles(attachments.ToList());
        }

        public MailBuilder EmbedImage(Stream stream, string name, string cid) {
            sendgrid.AddAttachment(stream, name);
            sendgrid.EmbedImage(name, cid);
            return this;
        }
        public MailBuilder EmbedImage(string filePath, string cid) {
            sendgrid.AddAttachment(filePath);
            sendgrid.EmbedImage(new FileInfo(filePath).Name, cid);
            return this;
        }
        public MailBuilder EmbedImage(LinkedResource resource) {
            return this.EmbedImage(resource.ContentStream, resource.ContentId, resource.ContentId);
        }
        public MailBuilder EmbedImages(IEnumerable<LinkedResource> resources) {
            foreach (var resource in resources) { this.EmbedImage(resource); }
            return this;
        }
        public MailBuilder EmbedImages(LinkedResourceCollection resources) {
            return this.EmbedImages(resources.ToList());
        }

        public MailBuilder AddHeader(string key, string value) {
            sendgrid.AddHeaders(new Dictionary<string, string> { { key, value } });
            return this;
        }
        public MailBuilder AddHeaders(IDictionary<string, string> headers) {
            sendgrid.AddHeaders(headers);
            return this;
        }
        public MailBuilder Substitute(string replacementTag, IEnumerable<string> substitutionValues) {
            sendgrid.AddSubstitution(replacementTag, substitutionValues.ToList());
            return this;
        }

        public MailBuilder IncludeUniqueArg(string key, string value) {
            sendgrid.AddUniqueArgs(new Dictionary<string, string> { { key, value } });
            return this;
        }
        public MailBuilder IncludeUniqueArgs(IDictionary<string, string> identifiers) {
            sendgrid.AddUniqueArgs(identifiers);
            return this;
        }

        public MailBuilder SetCategory(string category) {
            sendgrid.SetCategory(category);
            return this;
        }
        public MailBuilder SetCategories(IEnumerable<string> categories) {
            sendgrid.SetCategories(categories);
            return this;
        }

        public MailBuilder EnableGravatar() {
            sendgrid.EnableGravatar();
            return this;
        }
        public MailBuilder EnableOpenTracking() {
            sendgrid.EnableOpenTracking();
            return this;
        }
        public MailBuilder EnableClickTracking(bool includePlainText = false) {
            sendgrid.EnableClickTracking(includePlainText);
            return this;
        }
        public MailBuilder EnableSpamCheck(int score = 5, string url = null) {
            sendgrid.EnableSpamCheck(score, url);
            return this;
        }
        public MailBuilder EnableUnsubscribe(string text, string html) {
            // TODO: Because of a bug in SendGrid 3.0.0, we are directly running the corrected EnableUnsubscribe(text, html) code.  Remove once the new build of SendGrid is available.
            //sendgrid.EnableUnsubscribe(text, html);
            var filter = "subscriptiontrack";

            if (!TextUnsubscribeTest.IsMatch(text)) {
                throw new Exception("Missing substitution replacementTag in text");
            }

            if (!HtmlUnsubscribeTest.IsMatch(html)) {
                throw new Exception("Missing substitution replacementTag in html");
            }

            sendgrid.Header.EnableFilter(filter);
            sendgrid.Header.AddFilterSetting(filter, new List<string> { "text/plain" }, text);
            sendgrid.Header.AddFilterSetting(filter, new List<string> { "text/html" }, html);

            return this;
        }
        public MailBuilder EnableUnsubscribe(string replace) {
            sendgrid.EnableUnsubscribe(replace);
            return this;
        }
        public MailBuilder EnableFooter(string text = null, string html = null) {
            sendgrid.EnableFooter(text, html);
            return this;
        }
        public MailBuilder EnableGoogleAnalytics(string source, string medium, string term, string content = null, string campaign = null) {
            sendgrid.EnableGoogleAnalytics(source, medium, term, content, campaign);
            return this;
        }
        public MailBuilder EnableTemplate(string html) {
            sendgrid.EnableTemplate(html);
            return this;
        }
        public MailBuilder EnableBcc(string email) {
            sendgrid.EnableBcc(email);
            return this;
        }
        public MailBuilder EnableBypassListManagement() {
            sendgrid.EnableBypassListManagement();
            return this;
        }

        public MailBuilder DisableGravatar() {
            sendgrid.DisableGravatar();
            return this;
        }
        public MailBuilder DisableOpenTracking() {
            sendgrid.DisableOpenTracking();
            return this;
        }
        public MailBuilder DisableClickTracking() {
            sendgrid.DisableClickTracking();
            return this;
        }
        public MailBuilder DisableSpamCheck() {
            sendgrid.DisableSpamCheck();
            return this;
        }
        public MailBuilder DisableUnsubscribe() {
            sendgrid.DisableUnsubscribe();
            return this;
        }
        public MailBuilder DisableFooter() {
            sendgrid.DisableFooter();
            return this;
        }
        public MailBuilder DisableGoogleAnalytics() {
            sendgrid.DisableGoogleAnalytics();
            return this;
        }
        public MailBuilder DisableTemplate() {
            sendgrid.DisableTemplate();
            return this;
        }
        public MailBuilder DisableBcc() {
            sendgrid.DisableBcc();
            return this;
        }
        public MailBuilder DisableBypassListManagement() {
            sendgrid.DisableBypassListManagement();
            return this;
        }

        private string GetAlternateViewAsString(AlternateView view) {
            var dataStream = view.ContentStream;
            byte[] byteBuffer = new byte[dataStream.Length];
            var encoding = Encoding.GetEncoding(view.ContentType.CharSet);
            return encoding.GetString(byteBuffer, 0, dataStream.Read(byteBuffer, 0, byteBuffer.Length));
        }
        private static string EmailFormat(string displayName, string email) {
            return string.Format("{0} <{1}>", displayName, email);
        }
    }

    internal static class Extensions {
        public static IEnumerable<string> ToListString(this IEnumerable<MailAddress> addresses) {
            return addresses.ToList().ConvertAll<string>(address =>
                string.Format(
                    string.IsNullOrWhiteSpace(address.DisplayName) ? "{1}" : "{0} <{1}>",
                    address.DisplayName,
                    address.Address)
                );
        }
    }
}