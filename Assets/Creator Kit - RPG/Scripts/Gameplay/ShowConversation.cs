﻿using System.Collections;
using System.Collections.Generic;
using RPGM.Core;
using RPGM.Gameplay;
using RPGM.UI;
using UnityEngine;

namespace RPGM.Events
{
    /// <summary>
    /// This event will start a conversation with an NPC using a conversation script.
    /// </summary>
    /// <typeparam name="ShowConversation"></typeparam>
    public class ShowConversation : Event<ShowConversation>
    {
        public NPCController npc;

        public GameObject gameObject;

        public ConversationScript conversation;

        public string conversationItemKey;

        public override void Execute()
        {
            ConversationPiece ci;

            //default to first conversation item if no key is specified, else find the right conversation item.
            if (string.IsNullOrEmpty(conversationItemKey))
                ci = conversation.items[0];
            else
                ci = conversation.Get(conversationItemKey);

            //if this item contains an unstarted quest, schedule a start quest event for the quest.
            if (ci.quest != null)
            {
                if (!ci.quest.isStarted)
                {
                    var ev = Schedule.Add<StartQuest>(1);
                    ev.quest = ci.quest;
                    ev.npc = npc;
                }
                if (
                    ci.quest.isFinished &&
                    ci.quest.questCompletedConversation != null
                )
                {
                    ci = ci.quest.questCompletedConversation.items[0];
                }
            }

            var position = gameObject.transform.position;
            var sr = gameObject.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                // put position above the sprite BUT  still on the screen.
                position.y = sr.bounds.max.y;
                position.y = Mathf.Clamp(position.y, 0, 100);
            }

            //show the dialog
            model.dialog.Show(position, ci.text);
            var animator = gameObject.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetBool("Talk", true);
                var ev = Schedule.Add<StopTalking>(2);
                ev.animator = animator;
            }

            if (ci.audio != null)
            {
                UserInterfaceAudio.PlayClip(ci.audio);
            }

            //speak some gibberish at two speech syllables per word.
            UserInterfaceAudio
                .Speak(gameObject.GetInstanceID(),
                ci.text.Split(' ').Length * 2,
                1);

            //if this conversation item has an id, register it in the model.
            if (!string.IsNullOrEmpty(ci.id))
                model.RegisterConversation(gameObject, ci.id);

            //setup conversation choices, if any.
            if (ci.options.Count == 0)
            {
                //do nothing
            }
            else
            {
                //Create option buttons below the dialog.
                for (var i = 0; i < ci.options.Count; i++)
                {
                    model.dialog.SetButton(i, ci.options[i].text);
                }

                //if user pickes this option, schedule an event to show the new option.
                model.dialog.onButton += async (index) =>
                {
                    //hide the old text, so we can display the new.
                    model.dialog.Hide();

                    //This is the id of the next conversation piece.
                    var next = ci.options[index].targetId;

                    Debug.Log("next convo: " + next);

                    if (next == "CONNECTED")
                    {
                        bool isConnected = await model.web3.IsConnected();
                        if (!isConnected)
                        {
                            await model.web3.Connect();
                        }
                        string addr = await model.web3.GetAddress();
                        var c2 =
                            new ConversationPiece {
                                id = "CONNECTED_ADDR",
                                text =
                                    addr.Substring(0, 6) +
                                    "... ? What a weird name.",
                                options = new List<ConversationOption>()
                                // options = new System.Collections.Generic.List<ConversationOption> {
                                //     new ConversationOption {
                                //         text = "Continue",
                                //         targetId = "connect2"
                                //     }
                                // }
                            };

                        // conversation.Add(c);
                        // var c = conversation.Get(next);
                        //c.text = addr.Substring(0, 6) + "... ? What a weird name.";
                        conversation.Add (c2);
                        var ev = Schedule.Add<ShowConversation>(0.25f);
                        ev.conversation = conversation;
                        ev.gameObject = gameObject;
                        ev.conversationItemKey = c2.id;
                        return;
                    }

                    //Make sure it actually exists!
                    if (conversation.ContainsKey(next))
                    {
                        //find the conversation piece object and setup a new event with correct parameters.
                        var c = conversation.Get(next);
                        var ev = Schedule.Add<ShowConversation>(0.25f);
                        ev.conversation = conversation;
                        ev.gameObject = gameObject;
                        ev.conversationItemKey = next;
                    }
                    else
                    {
                        Debug.LogError($"No conversation with ID:{next}");
                    }
                };
            }

            //if conversation has an icon associated, this will display it.
            model.dialog.SetIcon(ci.image);
        }
    }
}