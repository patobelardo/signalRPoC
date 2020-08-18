"use strict";

var connection = new signalR.HubConnectionBuilder().withUrl("/chatHub").build();

//Disable send button until connection is established
document.getElementById("sendButton").disabled = true;
document.getElementById("sendPrivateButton").disabled = true;
document.getElementById("addGroupButton").disabled = true;
document.getElementById("loginButton").disabled = true;

connection.on("ReceiveMessage", function (user, message) {
    var msg = message.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    var encodedMsg = user + " says " + msg;
    var li = document.createElement("li");
    li.textContent = encodedMsg;
    if (encodedMsg.indexOf('info') != -1)
    {
        li.style.color = "green";
    }
    if (encodedMsg.indexOf('warning') != -1)
    {
        li.style.color = "orange";
    }
    if (encodedMsg.indexOf('error') != -1)
    {
        li.style.color = "red";
    }
    document.getElementById("messagesList").appendChild(li);
});

connection.on("LoggedUser"), function(user, message) {
    var msg = message.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    var encodedMsg = user + " says " + msg;
    var li = document.createElement("li");
    li.textContent = encodedMsg;
    document.getElementById("messagesList").appendChild(li);
};

connection.start().then(function () {
    document.getElementById("sendButton").disabled = false;
    document.getElementById("sendPrivateButton").disabled = false;
    document.getElementById("addGroupButton").disabled = false;
    document.getElementById("loginButton").disabled = false;
}).catch(function (err) {
    return console.error(err.toString());
});

document.getElementById("sendButton").addEventListener("click", function (event) {
    var user = document.getElementById("userInput").value;
    var message = document.getElementById("messageInput").value;
    connection.invoke("SendMessage", user, message).catch(function (err) {
        return console.error(err.toString());
    });
    event.preventDefault();
});

document.getElementById("sendPrivateButton").addEventListener("click", function (event) {
    var user = document.getElementById("userInput1").value;
    var message = document.getElementById("messageInput1").value;
    var destGroup = document.getElementById("destGroup1").value;
    connection.invoke("SendPrivateMessage", user, destGroup, message).catch(function (err) {
        return console.error(err.toString());
    });
    event.preventDefault();
});

document.getElementById("addGroupButton").addEventListener("click", function (event) {
    var group  = document.getElementById("groupInput").value;
    
    connection.invoke("AddToGroup", group).catch(function (err) {
        return console.error(err.toString());
    });
    var li = document.createElement("li");
    li.textContent = group;
    document.getElementById("groupList").appendChild(li);

    event.preventDefault();
});


document.getElementById("loginButton").addEventListener("click", function (event) {
    var userName  = document.getElementById("loginUserInput").value;
    
    connection.invoke("Login", userName).catch(function (err) {
        return console.error(err.toString());
    });

    event.preventDefault();
});