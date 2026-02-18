const { WebPubSubServiceClient } = require("@azure/web-pubsub");

module.exports = async function (context, req) {
	const connectionString = process.env.WEBPUBSUB_CONNECTION_STRING;
	
	if (!connectionString) {
		context.res = {
			status: 500,
			body: { error: "WEBPUBSUB_CONNECTION_STRING not configured" }
		};
		return;
	}

	try {
		const room = req.query.room || "default";
		const user = req.query.user || "anon";

		const client = new WebPubSubServiceClient(connectionString, "tbm");

		const token = await client.getClientAccessToken({
			userId: user,
			roles: [
				`webpubsub.joinLeaveGroup.${room}`,
				`webpubsub.sendToGroup.${room}`
			]
		});

		context.res = {
			status: 200,
			body: {
				url: token.url
			}
		};
	} catch (error) {
		context.res = {
			status: 500,
			body: { error: error.message }
		};
	}
};
