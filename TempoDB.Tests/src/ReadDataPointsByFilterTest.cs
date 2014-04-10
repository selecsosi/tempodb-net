using Moq;
using NodaTime;
using NUnit.Framework;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;
using TempoDB.Exceptions;


namespace TempoDB.Tests
{
    [TestFixture]
    class ReadDataPointsByFilterTest
    {
        private static DateTimeZone zone = DateTimeZone.Utc;

        private static string json = @"{
            ""rollup"":{
                ""fold"":""sum"",
                ""period"":""PT1H""
            },
            ""tz"":""UTC"",
            ""data"":[
                {""t"":""2012-01-01T00:00:01.000+00:00"",""v"":12.34}
            ]
        }";

        private static string json1 = @"{
            ""data"":[
                {""t"":""2012-03-27T00:00:00.000-05:00"",""v"":12.34},
                {""t"":""2012-03-27T00:01:00.000-05:00"",""v"":23.45}
            ],
            ""tz"":""UTC"",
            ""rollup"":null
        }";

        private static string json2 = @"{
            ""data"":[
                {""t"":""2012-03-27T00:02:00.000-05:00"",""v"":34.56}
            ],
            ""tz"":""UTC"",
            ""rollup"":null
        }";

        private static string jsonTz = @"{
            ""data"":[
                {""t"":""2012-03-27T00:00:00.000-05:00"",""v"":12.34},
                {""t"":""2012-03-27T00:01:00.000-05:00"",""v"":23.45}
            ],
            ""tz"":""America/Chicago"",
            ""rollup"":null
        }";

        private static ZonedDateTime start = zone.AtStrictly(new LocalDateTime(2012, 3, 27, 0, 0, 0));
        private static ZonedDateTime end = zone.AtStrictly(new LocalDateTime(2012, 3, 28, 0, 0, 0));
        private static Interval interval = new Interval(start.ToInstant(), end.ToInstant());
        private static Aggregation aggregation = new Aggregation(Fold.Sum);
        private static Filter filter = new Filter().AddKeys("key1");

        [Test]
        public void SmokeTest()
        {
            var response = TestCommon.GetResponse(200, json1);
            var client = TestCommon.GetClient(response);

            var result = client.ReadDataPoints(filter, interval, aggregation);

            var expected = new List<DataPoint> {
                new DataPoint(zone.AtStrictly(new LocalDateTime(2012, 3, 27, 5, 0, 0)), 12.34),
                new DataPoint(zone.AtStrictly(new LocalDateTime(2012, 3, 27, 5, 1, 0)), 23.45)
            };
            var output = new List<DataPoint>();
            foreach(DataPoint dp in result.Value.DataPoints)
            {
                output.Add(dp);
            }

            Assert.AreEqual(expected, output);
        }

        [Test]
        public void SmokeTestTz()
        {
            var zone = DateTimeZoneProviders.Tzdb["America/Chicago"];
            var response = TestCommon.GetResponse(200, jsonTz);
            var client = TestCommon.GetClient(response);

            var result = client.ReadDataPoints(filter, interval, aggregation, zone);

            var expected = new List<DataPoint> {
                new DataPoint(zone.AtStrictly(new LocalDateTime(2012, 3, 27, 0, 0, 0)), 12.34),
                new DataPoint(zone.AtStrictly(new LocalDateTime(2012, 3, 27, 0, 1, 0)), 23.45)
            };
            var output = new List<DataPoint>();
            foreach(DataPoint dp in result.Value.DataPoints)
            {
                output.Add(dp);
            }

            Assert.AreEqual(expected, output);
        }

        [Test]
        public void MultipleSegmentSmokeTest()
        {
            var response1 = TestCommon.GetResponse(200, json1);
            response1.Headers.Add(new Parameter {
                Name = "Link",
                Value = "</v1/segment/?key=key1&start=2012-03-27T00:02:00.000-05:00&end=2012-03-28>; rel=\"next\""
            });
            var response2 = TestCommon.GetResponse(200, json2);

            var calls = 0;
            RestResponse[] responses = { response1, response2 };
            var mockclient = new Mock<RestClient>();
            mockclient.Setup(cl => cl.Execute(It.IsAny<RestRequest>())).Returns(() => responses[calls]).Callback(() => calls++);

            var client = TestCommon.GetClient(mockclient.Object);
            var result = client.ReadDataPoints(filter, interval, aggregation);

            var expected = new List<DataPoint> {
                new DataPoint(zone.AtStrictly(new LocalDateTime(2012, 3, 27, 5, 0, 0)), 12.34),
                new DataPoint(zone.AtStrictly(new LocalDateTime(2012, 3, 27, 5, 1, 0)), 23.45),
                new DataPoint(zone.AtStrictly(new LocalDateTime(2012, 3, 27, 5, 2, 0)), 34.56)
            };
            var output = new List<DataPoint>();
            foreach(DataPoint dp in result.Value.DataPoints)
            {
                output.Add(dp);
            }

            Assert.AreEqual(expected, output);
        }

        [Test]
        public void RequestMethod()
        {
            var response = TestCommon.GetResponse(200, json);
            var mockclient = TestCommon.GetMockRestClient(response);
            var client = TestCommon.GetClient(mockclient.Object);

            client.ReadDataPoints(filter, interval, aggregation);

            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => req.Method == Method.GET)));
        }

        [Test]
        public void RequestUrl()
        {
            var response = TestCommon.GetResponse(200, json);
            var mockclient = TestCommon.GetMockRestClient(response);
            var client = TestCommon.GetClient(mockclient.Object);

            client.ReadDataPoints(filter, interval, aggregation);

            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => req.Resource == "/{version}/segment/")));
        }

        [Test]
        public void RequestParameters()
        {
            var response = TestCommon.GetResponse(200, json);
            var mockclient = TestCommon.GetMockRestClient(response);
            var client = TestCommon.GetClient(mockclient.Object);
            var start = zone.AtStrictly(new LocalDateTime(2012, 1, 1, 0, 0, 0));
            var end = zone.AtStrictly(new LocalDateTime(2012, 1, 2, 0, 0, 0));
            var interval = new Interval(start.ToInstant(), end.ToInstant());

            client.ReadDataPoints(filter, interval, aggregation);

            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "key", "key1"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "start", "2012-01-01T00:00:00+00:00"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "end", "2012-01-02T00:00:00+00:00"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "tz", "UTC"))));
        }

        [Test]
        public void RequestParametersRollup()
        {
            var response = TestCommon.GetResponse(200, json);
            var mockclient = TestCommon.GetMockRestClient(response);
            var client = TestCommon.GetClient(mockclient.Object);
            var start = zone.AtStrictly(new LocalDateTime(2012, 1, 1, 0, 0, 0));
            var end = zone.AtStrictly(new LocalDateTime(2012, 1, 2, 0, 0, 0));
            var interval = new Interval(start.ToInstant(), end.ToInstant());

            var rollup = new Rollup(Period.FromMinutes(1), Fold.Mean);
            client.ReadDataPoints(filter, interval, aggregation, rollup:rollup);

            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "key", "key1"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "start", "2012-01-01T00:00:00+00:00"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "end", "2012-01-02T00:00:00+00:00"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "tz", "UTC"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "rollup.period", "PT1M"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "rollup.fold", "mean"))));
        }

        [Test]
        public void RequestParametersRollupInterpolation()
        {
            var response = TestCommon.GetResponse(200, json);
            var mockclient = TestCommon.GetMockRestClient(response);
            var client = TestCommon.GetClient(mockclient.Object);
            var start = zone.AtStrictly(new LocalDateTime(2012, 1, 1, 0, 0, 0));
            var end = zone.AtStrictly(new LocalDateTime(2012, 1, 2, 0, 0, 0));
            var interval = new Interval(start.ToInstant(), end.ToInstant());

            var rollup = new Rollup(Period.FromMinutes(1), Fold.Mean);
            var interpolation = new Interpolation(Period.FromMinutes(1), InterpolationFunction.ZOH);
            client.ReadDataPoints(filter, interval, aggregation, rollup:rollup, interpolation:interpolation);

            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "key", "key1"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "start", "2012-01-01T00:00:00+00:00"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "end", "2012-01-02T00:00:00+00:00"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "tz", "UTC"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "rollup.period", "PT1M"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "rollup.fold", "mean"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "interpolation.period", "PT1M"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "interpolation.function", "zoh"))));
        }

        [Test]
        [ExpectedException(typeof(TempoDBException))]
        public void Error()
        {
            var response = TestCommon.GetResponse(403, "You are forbidden");
            var client = TestCommon.GetClient(response);

            var result = client.ReadDataPoints(filter, interval, aggregation);

            var output = new List<DataPoint>();
            foreach(DataPoint dp in result.Value.DataPoints)
            {
                output.Add(dp);
            }
        }
    }
}
