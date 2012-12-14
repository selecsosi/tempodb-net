﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using Moq;
using RestSharp;

namespace Client.Tests
{
    class TestCommon
    {
        public static Client GetClient(RestClient restClient = null)
        {
            return new ClientBuilder()
                                .Host("api.tempo-db.com")
                                .Key("api-key")
                                .Port(443)
                                .Secret("api-secret")
                                .Secure(true)
                                .RestClient(restClient)
                                .Build();
        }

        public static Mock<RestClient> GetMockRestClient<T>(T response, Expression<Func<RestRequest, bool>> requestValidator = null) where T : new()
        {
            if (requestValidator == null)
                requestValidator = req => true;

            var res = new RestSharp.RestResponse<T>
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                ResponseStatus = RestSharp.ResponseStatus.Completed,
                Data = response
            };

            var restClient = new Mock<RestClient>();
            restClient.Setup(cl => cl.Execute<T>(It.Is<RestRequest>(requestValidator))).Returns(res);
            return restClient;
        }

        public static Mock<RestClient> GetMockRestClient(Expression<Func<RestRequest, bool>> requestValidator = null)
        {
            if (requestValidator == null)
                requestValidator = req => true;

            var res = new RestSharp.RestResponse
            {
                StatusCode = System.Net.HttpStatusCode.OK
            };

            var restClient = new Mock<RestClient>();
            restClient.Setup(cl => cl.Execute(It.Is<RestRequest>(requestValidator))).Returns(res);
            return restClient;
        }

        public static bool ContainsParameter(List<Parameter> parameters, string name, string value)
        {
            foreach (var parameter in parameters)
            {
                if (parameter.Name.ToString() == name && parameter.Value.ToString() == value) return true;
            }
            return false;
        }
    }
}
