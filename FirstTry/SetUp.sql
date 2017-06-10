
-- EVENT
CREATE TABLE IF NOT EXISTS Event(
	id serial unique not null PRIMARY KEY,
	start_Time timestamp not null,
	end_Time timestamp not null,
	name text unique not null
);

CREATE UNIQUE INDEX IF NOT EXISTS Event_Index on Event(name);

INSERT INTO Event VALUES (0, make_timestamp(1,1,1,0,0,0),make_timestamp(2517,1,1,0,0,0),'');

CREATE OR REPLACE FUNCTION save_empty_event_f() 
	RETURNS TRIGGER AS $X$
	BEGIN
	IF (old.name = '') THEN 
		RETURN NULL; 
	ELSE 
		UPDATE Talk SET Event_Id = 1 WHERE Event_Id = old.id;
		RETURN OLD;
	END IF;
	END
	$X$ LANGUAGE plpgsql;

CREATE TRIGGER save_empty_event BEFORE DELETE ON Event
  FOR EACH ROW EXECUTE PROCEDURE save_empty_event_f();

  
  
-- PARTICIPANT/ORGANISER
CREATE TABLE IF NOT EXISTS Participant(
	id serial unique not null PRIMARY KEY,
	login text unique not null,
	password text not null
);

CREATE UNIQUE INDEX IF NOT EXISTS Participant_Index on Participant(login);

CREATE TABLE IF NOT EXISTS Organiser(
	id integer REFERENCES participant ON DELETE CASCADE PRIMARY KEY
);


--AGENDA
CREATE TABLE IF NOT EXISTS Agenda(
	id serial unique not null PRIMARY KEY,
	given_Id  text unique not null,
	talker_Id integer not null REFERENCES Participant ON DELETE CASCADE,
	start_Time timestamp not null,
	title text not null
);

CREATE INDEX IF NOT EXISTS Agenda_Index on Agenda(title);

CREATE TABLE IF NOT EXISTS Rejected_Agenda(
	id integer unique not null REFERENCES Agenda ON DELETE CASCADE PRIMARY KEY
);


--TALK
CREATE TABLE IF NOT EXISTS Talk(
	event_Id integer NOT NULL REFERENCES Event,  
	agenda_Id integer unique not null REFERENCES Agenda ON DELETE CASCADE PRIMARY KEY,
	organiser_Rating integer not null check (organiser_Rating >= 0 AND organiser_Rating <= 10),
	room text not null
);

CREATE OR REPLACE FUNCTION verify_talk_f() 
	RETURNS TRIGGER AS $X$
	DECLARE
	t0 timestamp;
	t1 timestamp;
	t timestamp;
	BEGIN
	SELECT start_Time,end_Time from Event where id = new.event_Id  INTO t0,t1;
	SELECT start_Time from Agenda where id = new.agenda_Id INTO t;
	IF (t < t0 OR t > t1) THEN 
		RETURN NULL; 
	ELSE 
		RETURN NEW;
	END IF;
	END
	$X$ LANGUAGE plpgsql;

CREATE TRIGGER verify_talk BEFORE INSERT ON Talk
  FOR EACH ROW EXECUTE PROCEDURE verify_talk_f();


--RELATIONS
CREATE TABLE IF NOT EXISTS Participant_Rates_Talk(
	participant_Id integer not null REFERENCES Participant ON DELETE CASCADE,
	talk_Id integer not null REFERENCES Agenda ON DELETE CASCADE,
	rating integer not null check (rating >= 0 AND rating <= 10),
	primary key (participant_Id, talk_Id)
);

CREATE TABLE IF NOT EXISTS Participant_Attends_Talk(
	participant_Id integer REFERENCES Participant ON DELETE CASCADE,
	talk_Id integer REFERENCES Agenda ON DELETE CASCADE,
	primary key (participant_Id, talk_Id)
);

CREATE TABLE IF NOT EXISTS Participant_SignsUpFor_Event(
	participant_Id integer REFERENCES Participant ON DELETE CASCADE,
	event_Id integer REFERENCES Event ON DELETE CASCADE,
	primary key (participant_Id, event_Id)
);

CREATE TABLE IF NOT EXISTS Participant_HasFriend_Participant(
	participant_Id1 integer REFERENCES participant ON DELETE CASCADE,
	participant_Id2 integer REFERENCES participant ON DELETE CASCADE,
	primary key (participant_Id1, participant_Id2)
);


