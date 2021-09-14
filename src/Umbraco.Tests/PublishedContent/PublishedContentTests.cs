using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Templates;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Serialization;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Extensions;
using Umbraco.Tests.TestHelpers;
using Umbraco.Web.Composing;

namespace Umbraco.Tests.PublishedContent
{
    /// <summary>
    /// Tests the methods on IPublishedContent using the DefaultPublishedContentStore
    /// </summary>
    [TestFixture]
    [UmbracoTest(TypeLoader = UmbracoTestOptions.TypeLoader.PerFixture)]
    public class PublishedContentTests : PublishedContentTestBase
    {
        protected override void Compose()
        {
            base.Compose();
            _publishedSnapshotAccessorMock = new Mock<IPublishedSnapshotAccessor>();
            Builder.Services.AddUnique<IPublishedSnapshotAccessor>(_publishedSnapshotAccessorMock.Object);

            Builder.Services.AddUnique<IPublishedModelFactory>(f => new PublishedModelFactory(f.GetRequiredService<TypeLoader>().GetTypes<PublishedContentModel>(), f.GetRequiredService<IPublishedValueFallback>()));
            Builder.Services.AddUnique<IPublishedContentTypeFactory, PublishedContentTypeFactory>();
            Builder.Services.AddUnique<IPublishedValueFallback, PublishedValueFallback>();

            var loggerFactory = NullLoggerFactory.Instance;
            var mediaService = Mock.Of<IMediaService>();
            var contentTypeBaseServiceProvider = Mock.Of<IContentTypeBaseServiceProvider>();
            var umbracoContextAccessor = Mock.Of<IUmbracoContextAccessor>();
            var backOfficeSecurityAccessor = Mock.Of<IBackOfficeSecurityAccessor>();
            var publishedUrlProvider = Mock.Of<IPublishedUrlProvider>();
            var imageSourceParser = new HtmlImageSourceParser(publishedUrlProvider);
            var serializer = new ConfigurationEditorJsonSerializer();
            var mediaFileService = new MediaFileManager(Mock.Of<IFileSystem>(), Mock.Of<IMediaPathScheme>(),
                loggerFactory.CreateLogger<MediaFileManager>(), Mock.Of<IShortStringHelper>());
            var pastedImages = new RichTextEditorPastedImages(umbracoContextAccessor, loggerFactory.CreateLogger<RichTextEditorPastedImages>(), HostingEnvironment, mediaService, contentTypeBaseServiceProvider, mediaFileService, ShortStringHelper, publishedUrlProvider, serializer);
            var linkParser = new HtmlLocalLinkParser(umbracoContextAccessor, publishedUrlProvider);

            var dataTypeService = new TestObjects.TestDataTypeService(
                new DataType(new VoidEditor(DataValueEditorFactory), serializer) { Id = 1 },
                new DataType(new TrueFalsePropertyEditor(DataValueEditorFactory, IOHelper), serializer) { Id = 1001 },
                new DataType(new RichTextPropertyEditor(DataValueEditorFactory, backOfficeSecurityAccessor, imageSourceParser, linkParser, pastedImages, IOHelper, Mock.Of<IImageUrlGenerator>()), serializer) { Id = 1002 },
                new DataType(new IntegerPropertyEditor(DataValueEditorFactory), serializer) { Id = 1003 },
                new DataType(new TextboxPropertyEditor(DataValueEditorFactory, IOHelper), serializer) { Id = 1004 },
                new DataType(new MediaPickerPropertyEditor(DataValueEditorFactory, IOHelper), serializer) { Id = 1005 });
            Builder.Services.AddUnique<IDataTypeService>(f => dataTypeService);
        }

        protected override void Initialize()
        {
            base.Initialize();

            var factory = Factory.GetRequiredService<IPublishedContentTypeFactory>() as PublishedContentTypeFactory;

            // need to specify a custom callback for unit tests
            // AutoPublishedContentTypes generates properties automatically
            // when they are requested, but we must declare those that we
            // explicitely want to be here...

            IEnumerable<IPublishedPropertyType> CreatePropertyTypes(IPublishedContentType contentType)
            {
                // AutoPublishedContentType will auto-generate other properties
                yield return factory.CreatePropertyType(contentType, "umbracoNaviHide", 1001);
                yield return factory.CreatePropertyType(contentType, "selectedNodes", 1);
                yield return factory.CreatePropertyType(contentType, "umbracoUrlAlias", 1);
                yield return factory.CreatePropertyType(contentType, "content", 1002);
                yield return factory.CreatePropertyType(contentType, "testRecursive", 1);
            }

            var compositionAliases = new[] { "MyCompositionAlias" };
            var anythingType = new AutoPublishedContentType(Guid.NewGuid(), 0, "anything", compositionAliases, CreatePropertyTypes);
            var homeType = new AutoPublishedContentType(Guid.NewGuid(), 0, "home", compositionAliases, CreatePropertyTypes);
            ContentTypesCache.GetPublishedContentTypeByAlias = alias => alias.InvariantEquals("home") ? homeType : anythingType;
        }


        protected override TypeLoader CreateTypeLoader(IIOHelper ioHelper, ITypeFinder typeFinder, IAppPolicyCache runtimeCache, ILogger<TypeLoader> logger, IProfilingLogger profilingLogger , IHostingEnvironment hostingEnvironment)
        {
            var baseLoader = base.CreateTypeLoader(ioHelper, typeFinder, runtimeCache, logger, profilingLogger , hostingEnvironment);

            return new TypeLoader(typeFinder, runtimeCache, new DirectoryInfo(hostingEnvironment.LocalTempPath), logger, profilingLogger , false,
                // this is so the model factory looks into the test assembly
                baseLoader.AssembliesToScan
                    .Union(new[] { typeof(PublishedContentTests).Assembly })
                    .ToList());
        }

        private readonly Guid _node1173Guid = Guid.NewGuid();
        private Mock<IPublishedSnapshotAccessor> _publishedSnapshotAccessorMock;

        protected override string GetXmlContent(int templateId)
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<!DOCTYPE root[
<!ELEMENT Home ANY>
<!ATTLIST Home id ID #REQUIRED>
<!ELEMENT CustomDocument ANY>
<!ATTLIST CustomDocument id ID #REQUIRED>
]>
<root id=""-1"">
    <Home id=""1046"" parentID=""-1"" level=""1"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""1"" createDate=""2012-06-12T14:13:17"" updateDate=""2012-07-20T18:50:43"" nodeName=""Home"" urlName=""home"" writerName=""admin"" creatorName=""admin"" path=""-1,1046"" isDoc="""">
        <content><![CDATA[]]></content>
        <umbracoUrlAlias><![CDATA[this/is/my/alias, anotheralias]]></umbracoUrlAlias>
        <umbracoNaviHide>1</umbracoNaviHide>
        <testRecursive><![CDATA[This is the recursive val]]></testRecursive>
        <Home id=""1173"" parentID=""1046"" level=""2"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""1"" createDate=""2012-07-20T18:06:45"" updateDate=""2012-07-20T19:07:31"" nodeName=""Sub1"" urlName=""sub1"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173"" isDoc="""" key=""" + _node1173Guid + @""">
            <content><![CDATA[<div>This is some content</div>]]></content>
            <umbracoUrlAlias><![CDATA[page2/alias, 2ndpagealias]]></umbracoUrlAlias>
            <testRecursive><![CDATA[]]></testRecursive>
            <Home id=""1174"" parentID=""1173"" level=""3"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""1"" createDate=""2012-07-20T18:07:54"" updateDate=""2012-07-20T19:10:27"" nodeName=""Sub2"" urlName=""sub2"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,1174"" isDoc="""">
                <content><![CDATA[]]></content>
                <umbracoUrlAlias><![CDATA[only/one/alias]]></umbracoUrlAlias>
                <creatorName><![CDATA[Custom data with same property name as the member name]]></creatorName>
                <testRecursive><![CDATA[]]></testRecursive>
            </Home>
			<CustomDocument id=""117"" parentID=""1173"" level=""3"" writerID=""0"" creatorID=""0"" nodeType=""1234"" template=""" + templateId + @""" sortOrder=""2"" createDate=""2018-07-18T10:06:37"" updateDate=""2018-07-18T10:06:37"" nodeName=""custom sub 1"" urlName=""custom-sub-1"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,117"" isDoc="""" />
			<CustomDocument id=""1177"" parentID=""1173"" level=""3"" writerID=""0"" creatorID=""0"" nodeType=""1234"" template=""" + templateId + @""" sortOrder=""3"" createDate=""2012-07-16T15:26:59"" updateDate=""2012-07-18T14:23:35"" nodeName=""custom sub 1"" urlName=""custom-sub-1"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,1177"" isDoc="""" />
			<CustomDocument id=""1178"" parentID=""1173"" level=""3"" writerID=""0"" creatorID=""0"" nodeType=""1234"" template=""" + templateId + @""" sortOrder=""4"" createDate=""2012-07-16T15:26:59"" updateDate=""2012-07-16T14:23:35"" nodeName=""custom sub 2"" urlName=""custom-sub-2"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,1178"" isDoc="""">
				<CustomDocument id=""1179"" parentID=""1178"" level=""4"" writerID=""0"" creatorID=""0"" nodeType=""1234"" template=""" + templateId + @""" sortOrder=""1"" createDate=""2012-07-16T15:26:59"" updateDate=""2012-07-18T14:23:35"" nodeName=""custom sub sub 1"" urlName=""custom-sub-sub-1"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,1178,1179"" isDoc="""" />
			</CustomDocument>
			<Home id=""1176"" parentID=""1173"" level=""3"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""5"" createDate=""2012-07-20T18:08:08"" updateDate=""2012-07-20T19:10:52"" nodeName=""Sub 3"" urlName=""sub-3"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1173,1176"" isDoc="""" key=""CDB83BBC-A83B-4BA6-93B8-AADEF67D3C09"">
                <content><![CDATA[]]></content>
                <umbracoNaviHide>1</umbracoNaviHide>
            </Home>
        </Home>
        <Home id=""1175"" parentID=""1046"" level=""2"" writerID=""0"" creatorID=""0"" nodeType=""1044"" template=""" + templateId + @""" sortOrder=""2"" createDate=""2012-07-20T18:08:01"" updateDate=""2012-07-20T18:49:32"" nodeName=""Sub 2"" urlName=""sub-2"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,1175"" isDoc=""""><content><![CDATA[]]></content>
        </Home>
        <CustomDocument id=""4444"" parentID=""1046"" level=""2"" writerID=""0"" creatorID=""0"" nodeType=""1234"" template=""" + templateId + @""" sortOrder=""3"" createDate=""2012-07-16T15:26:59"" updateDate=""2012-07-18T14:23:35"" nodeName=""Test"" urlName=""test-page"" writerName=""admin"" creatorName=""admin"" path=""-1,1046,4444"" isDoc="""">
            <selectedNodes><![CDATA[1172,1176,1173]]></selectedNodes>
        </CustomDocument>
    </Home>
    <CustomDocument id=""1172"" parentID=""-1"" level=""1"" writerID=""0"" creatorID=""0"" nodeType=""1234"" template=""" + templateId + @""" sortOrder=""2"" createDate=""2012-07-16T15:26:59"" updateDate=""2012-07-18T14:23:35"" nodeName=""Test"" urlName=""test-page"" writerName=""admin"" creatorName=""admin"" path=""-1,1172"" isDoc="""" />
</root>";
        }

        

        

        

        

        

        

        [Test]
        public void Children_GroupBy_DocumentTypeAlias()
        {
            var home = new AutoPublishedContentType(Guid.NewGuid(), 22, "Home", new PublishedPropertyType[] { });
            var custom = new AutoPublishedContentType(Guid.NewGuid(), 23, "CustomDocument", new PublishedPropertyType[] { });
            var contentTypes = new Dictionary<string, PublishedContentType>
            {
                { home.Alias, home },
                { custom.Alias, custom }
            };
            ContentTypesCache.GetPublishedContentTypeByAlias = alias => contentTypes[alias];

            var doc = GetNode(1046);

            var found1 = doc.Children(VariationContextAccessor).GroupBy(x => x.ContentType.Alias).ToArray();

            Assert.AreEqual(2, found1.Length);
            Assert.AreEqual(2, found1.Single(x => x.Key.ToString() == "Home").Count());
            Assert.AreEqual(1, found1.Single(x => x.Key.ToString() == "CustomDocument").Count());
        }

        [Test]
        public void Children_Where_DocumentTypeAlias()
        {
            var home = new AutoPublishedContentType(Guid.NewGuid(), 22, "Home", new PublishedPropertyType[] { });
            var custom = new AutoPublishedContentType(Guid.NewGuid(), 23, "CustomDocument", new PublishedPropertyType[] { });
            var contentTypes = new Dictionary<string, PublishedContentType>
            {
                { home.Alias, home },
                { custom.Alias, custom }
            };
            ContentTypesCache.GetPublishedContentTypeByAlias = alias => contentTypes[alias];

            var doc = GetNode(1046);

            var found1 = doc.Children(VariationContextAccessor).Where(x => x.ContentType.Alias == "CustomDocument");
            var found2 = doc.Children(VariationContextAccessor).Where(x => x.ContentType.Alias == "Home");

            Assert.AreEqual(1, found1.Count());
            Assert.AreEqual(2, found2.Count());
        }

        [Test]
        public void Children_Order_By_Update_Date()
        {
            var doc = GetNode(1173);

            var ordered = doc.Children(VariationContextAccessor).OrderBy(x => x.UpdateDate);

            var correctOrder = new[] { 1178, 1177, 1174, 1176 };
            for (var i = 0; i < correctOrder.Length; i++)
            {
                Assert.AreEqual(correctOrder[i], ordered.ElementAt(i).Id);
            }

        }

        [Test]
        public void FirstChild()
        {
            var doc = GetNode(1173); // has child nodes
            Assert.IsNotNull(doc.FirstChild(Mock.Of<IVariationContextAccessor>()));
            Assert.IsNotNull(doc.FirstChild(Mock.Of<IVariationContextAccessor>(), x => true));
            Assert.IsNotNull(doc.FirstChild<IPublishedContent>(Mock.Of<IVariationContextAccessor>()));

            doc = GetNode(1175); // does not have child nodes
            Assert.IsNull(doc.FirstChild(Mock.Of<IVariationContextAccessor>()));
            Assert.IsNull(doc.FirstChild(Mock.Of<IVariationContextAccessor>(), x => true));
            Assert.IsNull(doc.FirstChild<IPublishedContent>(Mock.Of<IVariationContextAccessor>()));
        }

        [Test]
        public void FirstChildAsT()
        {
            var doc = GetNode(1046); // has child nodes

            var model = doc.FirstChild<Home>(Mock.Of<IVariationContextAccessor>(), x => true); // predicate

            Assert.IsNotNull(model);
            Assert.IsTrue(model.Id == 1173);
            Assert.IsInstanceOf<Home>(model);
            Assert.IsInstanceOf<IPublishedContent>(model);

            doc = GetNode(1175); // does not have child nodes
            Assert.IsNull(doc.FirstChild<Anything>(Mock.Of<IVariationContextAccessor>()));
            Assert.IsNull(doc.FirstChild<Anything>(Mock.Of<IVariationContextAccessor>(), x => true));
        }

        [Test]
        public void IsComposedOf()
        {
            var doc = GetNode(1173);

            var isComposedOf = doc.IsComposedOf("MyCompositionAlias");

            Assert.IsTrue(isComposedOf);
        }

        [Test]
        public void HasProperty()
        {
            var doc = GetNode(1173);

            var hasProp = doc.HasProperty(Constants.Conventions.Content.UrlAlias);

            Assert.IsTrue(hasProp);
        }

        [Test]
        public void HasValue()
        {
            var doc = GetNode(1173);

            var hasValue = doc.HasValue(Mock.Of<IPublishedValueFallback>(), Constants.Conventions.Content.UrlAlias);
            var noValue = doc.HasValue(Mock.Of<IPublishedValueFallback>(), "blahblahblah");

            Assert.IsTrue(hasValue);
            Assert.IsFalse(noValue);
        }

        [Test]
        public void Ancestors_Where_Visible()
        {
            var doc = GetNode(1174);

            var whereVisible = doc.Ancestors().Where(x => x.IsVisible(Mock.Of<IPublishedValueFallback>()));
            Assert.AreEqual(1, whereVisible.Count());

        }

        [Test]
        public void Visible()
        {
            var hidden = GetNode(1046);
            var visible = GetNode(1173);

            Assert.IsFalse(hidden.IsVisible(Mock.Of<IPublishedValueFallback>()));
            Assert.IsTrue(visible.IsVisible(Mock.Of<IPublishedValueFallback>()));
        }

        [Test]
        public void Ancestor_Or_Self()
        {
            var doc = GetNode(1173);

            var result = doc.AncestorOrSelf();

            Assert.IsNotNull(result);

            // ancestor-or-self has to be self!
            Assert.AreEqual(1173, result.Id);
        }

        [Test]
        public void U4_4559()
        {
            var doc = GetNode(1174);
            var result = doc.AncestorOrSelf(1);
            Assert.IsNotNull(result);
            Assert.AreEqual(1046, result.Id);
        }

        [Test]
        public void Ancestors_Or_Self()
        {
            var doc = GetNode(1174);

            var result = doc.AncestorsOrSelf().ToArray();

            Assert.IsNotNull(result);

            Assert.AreEqual(3, result.Length);
            Assert.IsTrue(result.Select(x => ((dynamic)x).GetId()).ContainsAll(new dynamic[] { 1174, 1173, 1046 }));
        }

        [Test]
        public void Ancestors()
        {
            var doc = GetNode(1174);

            var result = doc.Ancestors().ToArray();

            Assert.IsNotNull(result);

            Assert.AreEqual(2, result.Length);
            Assert.IsTrue(result.Select(x => ((dynamic)x).GetId()).ContainsAll(new dynamic[] { 1173, 1046 }));
        }

        [Test]
        public void IsAncestor()
        {
            // Structure:
            // - Root : 1046 (no parent)
            // -- Home: 1173 (parent 1046)
            // -- Custom Doc: 1178 (parent 1173)
            // --- Custom Doc2: 1179 (parent: 1178)
            // -- Custom Doc4: 117 (parent 1173)
            // - Custom Doc3: 1172 (no parent)

            var home = GetNode(1173);
            var root = GetNode(1046);
            var customDoc = GetNode(1178);
            var customDoc2 = GetNode(1179);
            var customDoc3 = GetNode(1172);
            var customDoc4 = GetNode(117);

            Assert.IsTrue(root.IsAncestor(customDoc4));
            Assert.IsFalse(root.IsAncestor(customDoc3));
            Assert.IsTrue(root.IsAncestor(customDoc2));
            Assert.IsTrue(root.IsAncestor(customDoc));
            Assert.IsTrue(root.IsAncestor(home));
            Assert.IsFalse(root.IsAncestor(root));

            Assert.IsTrue(home.IsAncestor(customDoc4));
            Assert.IsFalse(home.IsAncestor(customDoc3));
            Assert.IsTrue(home.IsAncestor(customDoc2));
            Assert.IsTrue(home.IsAncestor(customDoc));
            Assert.IsFalse(home.IsAncestor(home));
            Assert.IsFalse(home.IsAncestor(root));

            Assert.IsFalse(customDoc.IsAncestor(customDoc4));
            Assert.IsFalse(customDoc.IsAncestor(customDoc3));
            Assert.IsTrue(customDoc.IsAncestor(customDoc2));
            Assert.IsFalse(customDoc.IsAncestor(customDoc));
            Assert.IsFalse(customDoc.IsAncestor(home));
            Assert.IsFalse(customDoc.IsAncestor(root));

            Assert.IsFalse(customDoc2.IsAncestor(customDoc4));
            Assert.IsFalse(customDoc2.IsAncestor(customDoc3));
            Assert.IsFalse(customDoc2.IsAncestor(customDoc2));
            Assert.IsFalse(customDoc2.IsAncestor(customDoc));
            Assert.IsFalse(customDoc2.IsAncestor(home));
            Assert.IsFalse(customDoc2.IsAncestor(root));

            Assert.IsFalse(customDoc3.IsAncestor(customDoc3));
        }

        [Test]
        public void IsAncestorOrSelf()
        {
            // Structure:
            // - Root : 1046 (no parent)
            // -- Home: 1173 (parent 1046)
            // -- Custom Doc: 1178 (parent 1173)
            // --- Custom Doc2: 1179 (parent: 1178)
            // -- Custom Doc4: 117 (parent 1173)
            // - Custom Doc3: 1172 (no parent)

            var home = GetNode(1173);
            var root = GetNode(1046);
            var customDoc = GetNode(1178);
            var customDoc2 = GetNode(1179);
            var customDoc3 = GetNode(1172);
            var customDoc4 = GetNode(117);

            Assert.IsTrue(root.IsAncestorOrSelf(customDoc4));
            Assert.IsFalse(root.IsAncestorOrSelf(customDoc3));
            Assert.IsTrue(root.IsAncestorOrSelf(customDoc2));
            Assert.IsTrue(root.IsAncestorOrSelf(customDoc));
            Assert.IsTrue(root.IsAncestorOrSelf(home));
            Assert.IsTrue(root.IsAncestorOrSelf(root));

            Assert.IsTrue(home.IsAncestorOrSelf(customDoc4));
            Assert.IsFalse(home.IsAncestorOrSelf(customDoc3));
            Assert.IsTrue(home.IsAncestorOrSelf(customDoc2));
            Assert.IsTrue(home.IsAncestorOrSelf(customDoc));
            Assert.IsTrue(home.IsAncestorOrSelf(home));
            Assert.IsFalse(home.IsAncestorOrSelf(root));

            Assert.IsFalse(customDoc.IsAncestorOrSelf(customDoc4));
            Assert.IsFalse(customDoc.IsAncestorOrSelf(customDoc3));
            Assert.IsTrue(customDoc.IsAncestorOrSelf(customDoc2));
            Assert.IsTrue(customDoc.IsAncestorOrSelf(customDoc));
            Assert.IsFalse(customDoc.IsAncestorOrSelf(home));
            Assert.IsFalse(customDoc.IsAncestorOrSelf(root));

            Assert.IsFalse(customDoc2.IsAncestorOrSelf(customDoc4));
            Assert.IsFalse(customDoc2.IsAncestorOrSelf(customDoc3));
            Assert.IsTrue(customDoc2.IsAncestorOrSelf(customDoc2));
            Assert.IsFalse(customDoc2.IsAncestorOrSelf(customDoc));
            Assert.IsFalse(customDoc2.IsAncestorOrSelf(home));
            Assert.IsFalse(customDoc2.IsAncestorOrSelf(root));

            Assert.IsTrue(customDoc4.IsAncestorOrSelf(customDoc4));
            Assert.IsTrue(customDoc3.IsAncestorOrSelf(customDoc3));
        }


        [Test]
        public void Descendants_Or_Self()
        {
            var doc = GetNode(1046);

            var result = doc.DescendantsOrSelf(Mock.Of<IVariationContextAccessor>()).ToArray();

            Assert.IsNotNull(result);

            Assert.AreEqual(10, result.Count());
            Assert.IsTrue(result.Select(x => ((dynamic)x).GetId()).ContainsAll(new dynamic[] { 1046, 1173, 1174, 1176, 1175 }));
        }

        [Test]
        public void Descendants()
        {
            var doc = GetNode(1046);

            var result = doc.Descendants(Mock.Of<IVariationContextAccessor>()).ToArray();

            Assert.IsNotNull(result);

            Assert.AreEqual(9, result.Count());
            Assert.IsTrue(result.Select(x => ((dynamic)x).GetId()).ContainsAll(new dynamic[] { 1173, 1174, 1176, 1175, 4444 }));
        }

        [Test]
        public void IsDescendant()
        {
            // Structure:
            // - Root : 1046 (no parent)
            // -- Home: 1173 (parent 1046)
            // -- Custom Doc: 1178 (parent 1173)
            // --- Custom Doc2: 1179 (parent: 1178)
            // -- Custom Doc4: 117 (parent 1173)
            // - Custom Doc3: 1172 (no parent)

            var home = GetNode(1173);
            var root = GetNode(1046);
            var customDoc = GetNode(1178);
            var customDoc2 = GetNode(1179);
            var customDoc3 = GetNode(1172);
            var customDoc4 = GetNode(117);

            Assert.IsFalse(root.IsDescendant(root));
            Assert.IsFalse(root.IsDescendant(home));
            Assert.IsFalse(root.IsDescendant(customDoc));
            Assert.IsFalse(root.IsDescendant(customDoc2));
            Assert.IsFalse(root.IsDescendant(customDoc3));
            Assert.IsFalse(root.IsDescendant(customDoc4));

            Assert.IsTrue(home.IsDescendant(root));
            Assert.IsFalse(home.IsDescendant(home));
            Assert.IsFalse(home.IsDescendant(customDoc));
            Assert.IsFalse(home.IsDescendant(customDoc2));
            Assert.IsFalse(home.IsDescendant(customDoc3));
            Assert.IsFalse(home.IsDescendant(customDoc4));

            Assert.IsTrue(customDoc.IsDescendant(root));
            Assert.IsTrue(customDoc.IsDescendant(home));
            Assert.IsFalse(customDoc.IsDescendant(customDoc));
            Assert.IsFalse(customDoc.IsDescendant(customDoc2));
            Assert.IsFalse(customDoc.IsDescendant(customDoc3));
            Assert.IsFalse(customDoc.IsDescendant(customDoc4));

            Assert.IsTrue(customDoc2.IsDescendant(root));
            Assert.IsTrue(customDoc2.IsDescendant(home));
            Assert.IsTrue(customDoc2.IsDescendant(customDoc));
            Assert.IsFalse(customDoc2.IsDescendant(customDoc2));
            Assert.IsFalse(customDoc2.IsDescendant(customDoc3));
            Assert.IsFalse(customDoc2.IsDescendant(customDoc4));

            Assert.IsFalse(customDoc3.IsDescendant(customDoc3));
        }

        [Test]
        public void IsDescendantOrSelf()
        {
            // Structure:
            // - Root : 1046 (no parent)
            // -- Home: 1173 (parent 1046)
            // -- Custom Doc: 1178 (parent 1173)
            // --- Custom Doc2: 1179 (parent: 1178)
            // -- Custom Doc4: 117 (parent 1173)
            // - Custom Doc3: 1172 (no parent)

            var home = GetNode(1173);
            var root = GetNode(1046);
            var customDoc = GetNode(1178);
            var customDoc2 = GetNode(1179);
            var customDoc3 = GetNode(1172);
            var customDoc4 = GetNode(117);

            Assert.IsTrue(root.IsDescendantOrSelf(root));
            Assert.IsFalse(root.IsDescendantOrSelf(home));
            Assert.IsFalse(root.IsDescendantOrSelf(customDoc));
            Assert.IsFalse(root.IsDescendantOrSelf(customDoc2));
            Assert.IsFalse(root.IsDescendantOrSelf(customDoc3));
            Assert.IsFalse(root.IsDescendantOrSelf(customDoc4));

            Assert.IsTrue(home.IsDescendantOrSelf(root));
            Assert.IsTrue(home.IsDescendantOrSelf(home));
            Assert.IsFalse(home.IsDescendantOrSelf(customDoc));
            Assert.IsFalse(home.IsDescendantOrSelf(customDoc2));
            Assert.IsFalse(home.IsDescendantOrSelf(customDoc3));
            Assert.IsFalse(home.IsDescendantOrSelf(customDoc4));

            Assert.IsTrue(customDoc.IsDescendantOrSelf(root));
            Assert.IsTrue(customDoc.IsDescendantOrSelf(home));
            Assert.IsTrue(customDoc.IsDescendantOrSelf(customDoc));
            Assert.IsFalse(customDoc.IsDescendantOrSelf(customDoc2));
            Assert.IsFalse(customDoc.IsDescendantOrSelf(customDoc3));
            Assert.IsFalse(customDoc.IsDescendantOrSelf(customDoc4));

            Assert.IsTrue(customDoc2.IsDescendantOrSelf(root));
            Assert.IsTrue(customDoc2.IsDescendantOrSelf(home));
            Assert.IsTrue(customDoc2.IsDescendantOrSelf(customDoc));
            Assert.IsTrue(customDoc2.IsDescendantOrSelf(customDoc2));
            Assert.IsFalse(customDoc2.IsDescendantOrSelf(customDoc3));
            Assert.IsFalse(customDoc2.IsDescendantOrSelf(customDoc4));

            Assert.IsTrue(customDoc3.IsDescendantOrSelf(customDoc3));
        }

        [Test]
        public void SiblingsAndSelf()
        {
            // Structure:
            // - Root : 1046 (no parent)
            // -- Level1.1: 1173 (parent 1046)
            // --- Level1.1.1: 1174 (parent 1173)
            // --- Level1.1.2: 117 (parent 1173)
            // --- Level1.1.3: 1177 (parent 1173)
            // --- Level1.1.4: 1178 (parent 1173)
            // --- Level1.1.5: 1176 (parent 1173)
            // -- Level1.2: 1175 (parent 1046)
            // -- Level1.3: 4444 (parent 1046)
            var root = GetNode(1046);
            var level1_1 = GetNode(1173);
            var level1_1_1 = GetNode(1174);
            var level1_1_2 = GetNode(117);
            var level1_1_3 = GetNode(1177);
            var level1_1_4 = GetNode(1178);
            var level1_1_5 = GetNode(1176);
            var level1_2 = GetNode(1175);
            var level1_3 = GetNode(4444);

            _publishedSnapshotAccessorMock.Setup(x => x.PublishedSnapshot.Content.GetAtRoot(It.IsAny<string>())).Returns(new []{root});

            var variationContextAccessor = Factory.GetRequiredService<IVariationContextAccessor>();
            var publishedSnapshot = _publishedSnapshotAccessorMock.Object.PublishedSnapshot;

            CollectionAssertAreEqual(new []{root}, root.SiblingsAndSelf(publishedSnapshot, variationContextAccessor));

            CollectionAssertAreEqual( new []{level1_1, level1_2, level1_3}, level1_1.SiblingsAndSelf(publishedSnapshot, variationContextAccessor));
            CollectionAssertAreEqual( new []{level1_1, level1_2, level1_3}, level1_2.SiblingsAndSelf(publishedSnapshot, variationContextAccessor));
            CollectionAssertAreEqual( new []{level1_1, level1_2, level1_3}, level1_3.SiblingsAndSelf(publishedSnapshot, variationContextAccessor));

            CollectionAssertAreEqual( new []{level1_1_1, level1_1_2, level1_1_3, level1_1_4, level1_1_5}, level1_1_1.SiblingsAndSelf(publishedSnapshot, variationContextAccessor));
            CollectionAssertAreEqual( new []{level1_1_1, level1_1_2, level1_1_3, level1_1_4, level1_1_5}, level1_1_2.SiblingsAndSelf(publishedSnapshot, variationContextAccessor));
            CollectionAssertAreEqual( new []{level1_1_1, level1_1_2, level1_1_3, level1_1_4, level1_1_5}, level1_1_3.SiblingsAndSelf(publishedSnapshot, variationContextAccessor));
            CollectionAssertAreEqual( new []{level1_1_1, level1_1_2, level1_1_3, level1_1_4, level1_1_5}, level1_1_4.SiblingsAndSelf(publishedSnapshot, variationContextAccessor));
            CollectionAssertAreEqual( new []{level1_1_1, level1_1_2, level1_1_3, level1_1_4, level1_1_5}, level1_1_5.SiblingsAndSelf(publishedSnapshot, variationContextAccessor));

        }

         [Test]
        public void Siblings()
        {
            // Structure:
            // - Root : 1046 (no parent)
            // -- Level1.1: 1173 (parent 1046)
            // --- Level1.1.1: 1174 (parent 1173)
            // --- Level1.1.2: 117 (parent 1173)
            // --- Level1.1.3: 1177 (parent 1173)
            // --- Level1.1.4: 1178 (parent 1173)
            // --- Level1.1.5: 1176 (parent 1173)
            // -- Level1.2: 1175 (parent 1046)
            // -- Level1.3: 4444 (parent 1046)
            var root = GetNode(1046);
            var level1_1 = GetNode(1173);
            var level1_1_1 = GetNode(1174);
            var level1_1_2 = GetNode(117);
            var level1_1_3 = GetNode(1177);
            var level1_1_4 = GetNode(1178);
            var level1_1_5 = GetNode(1176);
            var level1_2 = GetNode(1175);
            var level1_3 = GetNode(4444);

            _publishedSnapshotAccessorMock.Setup(x => x.PublishedSnapshot.Content.GetAtRoot(It.IsAny<string>())).Returns(new []{root});

            var variationContextAccessor = Factory.GetRequiredService<IVariationContextAccessor>();
            var publishedSnapshot = _publishedSnapshotAccessorMock.Object.PublishedSnapshot;

            CollectionAssertAreEqual(new IPublishedContent[0], root.Siblings(publishedSnapshot, variationContextAccessor));

            CollectionAssertAreEqual( new []{level1_2, level1_3}, level1_1.Siblings(publishedSnapshot, variationContextAccessor));
            CollectionAssertAreEqual( new []{level1_1,  level1_3}, level1_2.Siblings(publishedSnapshot, variationContextAccessor));
            CollectionAssertAreEqual( new []{level1_1, level1_2}, level1_3.Siblings(publishedSnapshot, variationContextAccessor));

            CollectionAssertAreEqual( new []{ level1_1_2, level1_1_3, level1_1_4, level1_1_5}, level1_1_1.Siblings(publishedSnapshot, variationContextAccessor));
            CollectionAssertAreEqual( new []{level1_1_1,  level1_1_3, level1_1_4, level1_1_5}, level1_1_2.Siblings(publishedSnapshot, variationContextAccessor));
            CollectionAssertAreEqual( new []{level1_1_1, level1_1_2,  level1_1_4, level1_1_5}, level1_1_3.Siblings(publishedSnapshot, variationContextAccessor));
            CollectionAssertAreEqual( new []{level1_1_1, level1_1_2, level1_1_3,  level1_1_5}, level1_1_4.Siblings(publishedSnapshot, variationContextAccessor));
            CollectionAssertAreEqual( new []{level1_1_1, level1_1_2, level1_1_3, level1_1_4}, level1_1_5.Siblings(publishedSnapshot, variationContextAccessor));

        }

        private void CollectionAssertAreEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        where T: IPublishedContent
        {
            var e = expected.Select(x => x.Id);
            var a = actual.Select(x => x.Id);
            CollectionAssert.AreEquivalent(e, a, $"\nExpected:\n{string.Join(", ", e)}\n\nActual:\n{string.Join(", ", a)}");
        }

        [Test]
        public void FragmentProperty()
        {
            var factory = Factory.GetRequiredService<IPublishedContentTypeFactory>() as PublishedContentTypeFactory;

            IEnumerable<IPublishedPropertyType> CreatePropertyTypes(IPublishedContentType contentType)
            {
                yield return factory.CreatePropertyType(contentType, "detached", 1003);
            }

            var ct = factory.CreateContentType(Guid.NewGuid(), 0, "alias", CreatePropertyTypes);
            var pt = ct.GetPropertyType("detached");
            var prop = new PublishedElementPropertyBase(pt, null, false, PropertyCacheLevel.None, 5548);
            Assert.IsInstanceOf<int>(prop.GetValue());
            Assert.AreEqual(5548, prop.GetValue());
        }

        public void Fragment1()
        {
            var type = ContentTypesCache.Get(PublishedItemType.Content, "detachedSomething");
            var values = new Dictionary<string, object>();
            var f = new PublishedElement(type, Guid.NewGuid(), values, false);
        }

        [Test]
        public void Fragment2()
        {
            var factory = Factory.GetRequiredService<IPublishedContentTypeFactory>() as PublishedContentTypeFactory;

            IEnumerable<IPublishedPropertyType> CreatePropertyTypes(IPublishedContentType contentType)
            {
                yield return factory.CreatePropertyType(contentType, "legend", 1004);
                yield return factory.CreatePropertyType(contentType, "image", 1005);
                yield return factory.CreatePropertyType(contentType, "size", 1003);
            }

            const string val1 = "boom bam";
            const int val2 = 0;
            const int val3 = 666;

            var guid = Guid.NewGuid();

            var ct = factory.CreateContentType(Guid.NewGuid(), 0, "alias", CreatePropertyTypes);

            var c = new ImageWithLegendModel(ct, guid, new Dictionary<string, object>
            {
                { "legend", val1 },
                { "image", val2 },
                { "size", val3 },
            }, false);

            Assert.AreEqual(val1, c.Legend);
            Assert.AreEqual(val3, c.Size);
        }

        class ImageWithLegendModel : PublishedElement
        {
            public ImageWithLegendModel(IPublishedContentType contentType, Guid fragmentKey, Dictionary<string, object> values, bool previewing)
                : base(contentType, fragmentKey, values, previewing)
            { }


            public string Legend => this.Value<string>(Mock.Of<IPublishedValueFallback>(), "legend");

            public IPublishedContent Image => this.Value<IPublishedContent>(Mock.Of<IPublishedValueFallback>(), "image");

            public int Size => this.Value<int>(Mock.Of<IPublishedValueFallback>(), "size");
        }
    }
}
